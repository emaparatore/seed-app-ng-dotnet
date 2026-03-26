using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Seed.Application;
using Seed.Infrastructure;
using Seed.Infrastructure.Persistence.Seeders;
using Seed.Shared.Configuration;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddSerilog((services, configuration) =>
    configuration.ReadFrom.Configuration(builder.Configuration)
                 .ReadFrom.Services(services));

var host = builder.Build();

ValidateConfiguration(host.Services, host.Services.GetRequiredService<IHostEnvironment>());

await SeedApplicationDataAsync(host.Services);

static void ValidateConfiguration(IServiceProvider services, IHostEnvironment environment)
{
    if (!environment.IsProduction())
    {
        return;
    }

    var configuration = services.GetRequiredService<IConfiguration>();
    var jwtSettings = configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>();
    if (jwtSettings is null || string.IsNullOrEmpty(jwtSettings.Secret) || jwtSettings.Secret.Contains("ForDevelopmentOnly"))
    {
        throw new InvalidOperationException(
            "JWT Secret must be configured for production. Set the JwtSettings__Secret environment variable.");
    }

    var connectionString = configuration.GetConnectionString("DefaultConnection");
    if (string.IsNullOrEmpty(connectionString) || connectionString.Contains("seed_password"))
    {
        throw new InvalidOperationException(
            "Database connection string must be configured for production. Set the ConnectionStrings__DefaultConnection environment variable.");
    }
}

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
