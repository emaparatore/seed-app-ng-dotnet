using Microsoft.AspNetCore.Hosting;
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
    }
}
