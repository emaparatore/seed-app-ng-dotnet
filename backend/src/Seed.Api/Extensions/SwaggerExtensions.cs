using Asp.Versioning.ApiExplorer;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using Seed.Api.Configuration;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Seed.Api.Extensions;

public static class SwaggerExtensions
{
    public static IServiceCollection AddSwagger(this IServiceCollection services)
    {
        services.AddTransient<IConfigureOptions<SwaggerGenOptions>, ConfigureSwaggerOptions>();

        services.AddSwaggerGen(options =>
        {
            var jwtScheme = new OpenApiSecurityScheme
            {
                BearerFormat = "JWT",
                Name = "Authorization",
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                Description = "Enter your JWT Bearer token."
            };

            options.AddSecurityDefinition("Bearer", jwtScheme);
            options.AddSecurityRequirement(_ => new OpenApiSecurityRequirement
            {
                { new OpenApiSecuritySchemeReference("Bearer"), [] }
            });
        });

        return services;
    }

    public static WebApplication UseSwaggerWithUI(this WebApplication app)
    {
        app.UseSwagger();

        var provider = app.Services.GetRequiredService<IApiVersionDescriptionProvider>();

        app.UseSwaggerUI(options =>
        {
            foreach (var description in provider.ApiVersionDescriptions)
            {
                options.SwaggerEndpoint(
                    $"/swagger/{description.GroupName}/swagger.json",
                    $"Seed API {description.GroupName.ToUpperInvariant()}");
            }
        });

        return app;
    }
}
