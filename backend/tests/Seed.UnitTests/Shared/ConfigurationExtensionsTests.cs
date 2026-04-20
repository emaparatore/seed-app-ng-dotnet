using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Seed.Shared.Extensions;

namespace Seed.UnitTests.Shared;

public class ConfigurationExtensionsTests
{
    [Fact]
    public void IsPaymentsModuleEnabled_WhenEnabled_ReturnsTrue()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Modules:Payments:Enabled"] = "true"
            })
            .Build();

        configuration.IsPaymentsModuleEnabled().Should().BeTrue();
    }

    [Fact]
    public void IsPaymentsModuleEnabled_WhenDisabled_ReturnsFalse()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Modules:Payments:Enabled"] = "false"
            })
            .Build();

        configuration.IsPaymentsModuleEnabled().Should().BeFalse();
    }

    [Fact]
    public void IsPaymentsModuleEnabled_WhenSectionMissing_ReturnsFalse()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        configuration.IsPaymentsModuleEnabled().Should().BeFalse();
    }
}
