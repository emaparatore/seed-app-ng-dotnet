using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Seed.Application.Common.Interfaces;
using Seed.Infrastructure.Services.Payments;
using Seed.IntegrationTests.Infrastructure;

namespace Seed.IntegrationTests.Webhooks;

public class WebhookWebApplicationFactory : CustomWebApplicationFactory
{
    public const string TestWebhookSecret = "whsec_test_secret_for_integration_tests";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.UseSetting("Modules:Payments:Enabled", "true");
        builder.UseSetting("Modules:Payments:Provider", "Stripe");
        builder.UseSetting("Stripe:SecretKey", "sk_test_fake_for_webhook_tests");
        builder.UseSetting("Stripe:PublishableKey", "pk_test_fake");
        builder.UseSetting("Stripe:WebhookSecret", TestWebhookSecret);

        builder.ConfigureTestServices(services =>
        {
            var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IPaymentGateway));
            if (descriptor is not null)
                services.Remove(descriptor);
            services.AddScoped<IPaymentGateway, MockPaymentGateway>();
        });
    }
}
