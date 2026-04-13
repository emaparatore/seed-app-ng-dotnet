using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;
using Asp.Versioning;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Seed.Api.Authorization;
using Seed.Api.Extensions;
using Seed.Api.Middleware;
using Seed.Application;
using Seed.Application.Common.Interfaces;
using Seed.Infrastructure;
using Seed.Infrastructure.Persistence;
using Seed.Infrastructure.Persistence.Seeders;
using Serilog;
using Seed.Shared.Configuration;
using Prometheus;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, configuration) =>
    configuration.ReadFrom.Configuration(context.Configuration));

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services.AddControllers();

var jwtSettings = builder.Configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>()!;
builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidAudience = jwtSettings.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Secret)),
            ClockSkew = TimeSpan.Zero,
            RoleClaimType = ClaimTypes.Role
        };

        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var blacklistService = context.HttpContext.RequestServices
                    .GetRequiredService<ITokenBlacklistService>();

                var userIdClaim = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
                var iatClaim = context.Principal?.FindFirstValue(JwtRegisteredClaimNames.Iat);

                if (userIdClaim is not null && iatClaim is not null)
                {
                    var userId = Guid.Parse(userIdClaim);
                    var issuedAt = DateTimeOffset.FromUnixTimeSeconds(long.Parse(iatClaim)).UtcDateTime;

                    if (await blacklistService.IsUserTokenBlacklistedAsync(userId, issuedAt))
                    {
                        context.Fail("Token has been revoked.");
                    }
                }
            }
        };
    });

builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionAuthorizationPolicyProvider>();
builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
builder.Services.AddScoped<IAuthorizationHandler, RequiresPlanAuthorizationHandler>();
builder.Services.AddScoped<IAuthorizationHandler, RequiresFeatureAuthorizationHandler>();

builder.Services
    .AddApiVersioning(options =>
    {
        options.DefaultApiVersion = new ApiVersion(1, 0);
        options.AssumeDefaultVersionWhenUnspecified = true;
        options.ReportApiVersions = true;
    })
    .AddApiExplorer(options =>
    {
        options.GroupNameFormat = "'v'VVV";
        options.SubstituteApiVersionInUrl = true;
    });

builder.Services.AddSwagger();

builder.Services.AddHealthChecks()
    .AddNpgSql(
        builder.Configuration.GetConnectionString("DefaultConnection")!,
        name: "postgresql",
        tags: ["db", "ready"]);

var allowedOrigins = builder.Configuration.GetSection("CorsSettings:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowedOrigins", policy =>
    {
        if (allowedOrigins.Length > 0)
        {
            policy.WithOrigins(allowedOrigins)
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials();
        }
    });
});

var rateLimitAuth = builder.Configuration.GetValue("RateLimiting:Auth:PermitLimit", 10);
var rateLimitAuthSensitive = builder.Configuration.GetValue("RateLimiting:AuthSensitive:PermitLimit", 5);

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddFixedWindowLimiter("auth", limiter =>
    {
        limiter.PermitLimit = rateLimitAuth;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueLimit = 0;
    });

    options.AddFixedWindowLimiter("auth-sensitive", limiter =>
    {
        limiter.PermitLimit = rateLimitAuthSensitive;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueLimit = 0;
    });

    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.ContentType = "application/json";
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            context.HttpContext.Response.Headers.RetryAfter = ((int)retryAfter.TotalSeconds).ToString();
        }
        await context.HttpContext.Response.WriteAsJsonAsync(new
        {
            errors = new[] { "Too many requests. Please try again later." }
        }, cancellationToken);
    };
});

var app = builder.Build();

if (app.Environment.IsProduction())
{
    if (string.IsNullOrEmpty(jwtSettings.Secret) || jwtSettings.Secret.Contains("ForDevelopmentOnly"))
        throw new InvalidOperationException(
            "JWT Secret must be configured for production. Set the JwtSettings__Secret environment variable.");

    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    if (string.IsNullOrEmpty(connectionString) || connectionString.Contains("seed_password"))
        throw new InvalidOperationException(
            "Database connection string must be configured for production. Set the ConnectionStrings__DefaultConnection environment variable.");
}

if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await dbContext.Database.MigrateAsync();
    await SeedApplicationDataAsync(app.Services);

    app.UseSwaggerWithUI();
}

app.UseExceptionHandler();
app.UseSerilogRequestLogging();
app.UseHttpsRedirection();
app.UseCors("AllowedOrigins");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<MustChangePasswordMiddleware>();
app.UseHttpMetrics();

app.MapControllers();

app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var result = new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description,
                duration = e.Value.Duration.TotalMilliseconds
            }),
            totalDuration = report.TotalDuration.TotalMilliseconds
        };
        await context.Response.WriteAsJsonAsync(result);
    }
});

app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});

app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = _ => false
});

app.MapMetrics();

app.Run();

static async Task SeedApplicationDataAsync(IServiceProvider services)
{
    using var scope = services.CreateScope();

    var rolesSeeder = scope.ServiceProvider.GetRequiredService<RolesAndPermissionsSeeder>();
    await rolesSeeder.SeedAsync();

    var adminSeeder = scope.ServiceProvider.GetRequiredService<SuperAdminSeeder>();
    await adminSeeder.SeedAsync();

    var settingsSeeder = scope.ServiceProvider.GetRequiredService<SystemSettingsSeeder>();
    await settingsSeeder.SeedAsync();
}
