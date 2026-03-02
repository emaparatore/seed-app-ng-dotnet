using Asp.Versioning.ApiExplorer;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Seed.Api.Configuration;

public sealed class ConfigureSwaggerOptions(IApiVersionDescriptionProvider provider)
    : IConfigureOptions<SwaggerGenOptions>
{
    public void Configure(SwaggerGenOptions options)
    {
        foreach (var description in provider.ApiVersionDescriptions)
            options.SwaggerDoc(description.GroupName, CreateInfo(description));
    }

    private static OpenApiInfo CreateInfo(ApiVersionDescription description) => new()
    {
        Title = "Seed API",
        Version = description.ApiVersion.ToString(),
        Description = description.IsDeprecated
            ? "This API version has been deprecated."
            : "Seed application REST API."
    };
}
