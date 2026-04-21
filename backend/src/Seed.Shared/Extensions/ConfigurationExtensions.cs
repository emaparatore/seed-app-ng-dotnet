using Microsoft.Extensions.Configuration;

namespace Seed.Shared.Extensions;

public static class ConfigurationExtensions
{
    public static bool IsPaymentsModuleEnabled(this IConfiguration configuration)
    {
        return configuration.GetValue<bool>("Modules:Payments:Enabled");
    }
}
