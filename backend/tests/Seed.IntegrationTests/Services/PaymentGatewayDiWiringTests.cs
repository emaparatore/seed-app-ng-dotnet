using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Seed.Application.Common.Interfaces;
using Seed.Infrastructure;
using Seed.Infrastructure.Services.Payments;

namespace Seed.IntegrationTests.Services;

public class PaymentGatewayDiWiringTests
{
    private static ServiceProvider BuildProvider(Dictionary<string, string?> config)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(config)
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure(configuration);

        return services.BuildServiceProvider();
    }

    [Fact]
    public void WhenStripeProviderWithSecretKey_ResolvesStripePaymentGateway()
    {
        using var provider = BuildProvider(new Dictionary<string, string?>
        {
            ["Modules:Payments:Enabled"] = "true",
            ["Modules:Payments:Provider"] = "Stripe",
            ["Stripe:SecretKey"] = "sk_test_fake_key_for_di_test",
            ["Stripe:PublishableKey"] = "pk_test_fake",
            ["Stripe:WebhookSecret"] = "whsec_fake",
            // DB connection needed for AddInfrastructure, but we won't resolve DbContext
            ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=fake;Username=fake;Password=fake",
        });

        using var scope = provider.CreateScope();
        var gateway = scope.ServiceProvider.GetService<IPaymentGateway>();

        gateway.Should().NotBeNull();
        gateway.Should().BeOfType<StripePaymentGateway>();
    }

    [Fact]
    public void WhenStripeProviderWithoutSecretKey_ResolvesMockPaymentGateway()
    {
        using var provider = BuildProvider(new Dictionary<string, string?>
        {
            ["Modules:Payments:Enabled"] = "true",
            ["Modules:Payments:Provider"] = "Stripe",
            ["Stripe:SecretKey"] = "",
            ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=fake;Username=fake;Password=fake",
        });

        using var scope = provider.CreateScope();
        var gateway = scope.ServiceProvider.GetService<IPaymentGateway>();

        gateway.Should().NotBeNull();
        gateway.Should().BeOfType<MockPaymentGateway>();
    }

    [Fact]
    public void WhenNonStripeProvider_ResolvesMockPaymentGateway()
    {
        using var provider = BuildProvider(new Dictionary<string, string?>
        {
            ["Modules:Payments:Enabled"] = "true",
            ["Modules:Payments:Provider"] = "Other",
            ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=fake;Username=fake;Password=fake",
        });

        using var scope = provider.CreateScope();
        var gateway = scope.ServiceProvider.GetService<IPaymentGateway>();

        gateway.Should().NotBeNull();
        gateway.Should().BeOfType<MockPaymentGateway>();
    }

    [Fact]
    public void WhenPaymentsDisabled_PaymentGatewayNotRegistered()
    {
        using var provider = BuildProvider(new Dictionary<string, string?>
        {
            ["Modules:Payments:Enabled"] = "false",
            ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=fake;Username=fake;Password=fake",
        });

        using var scope = provider.CreateScope();
        var gateway = scope.ServiceProvider.GetService<IPaymentGateway>();

        gateway.Should().BeNull();
    }
}
