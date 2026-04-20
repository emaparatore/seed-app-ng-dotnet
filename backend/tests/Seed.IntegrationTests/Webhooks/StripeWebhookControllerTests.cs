using System.Net;
using System.Text;
using FluentAssertions;
using Seed.IntegrationTests.Infrastructure;
using Stripe;

namespace Seed.IntegrationTests.Webhooks;

public class StripeWebhookControllerTests : IClassFixture<WebhookWebApplicationFactory>
{
    private readonly HttpClient _client;

    public StripeWebhookControllerTests(WebhookWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task PostWebhook_InvalidSignature_Returns400()
    {
        var payload = """{"id":"evt_test","object":"event","type":"checkout.session.completed"}""";
        var content = new StringContent(payload, Encoding.UTF8, "application/json");
        content.Headers.Add("Stripe-Signature", "t=1234567890,v1=invalidsignature");

        var response = await _client.PostAsync("/webhooks/stripe", content);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PostWebhook_ValidSignature_Returns200()
    {
        var payload = BuildMinimalEventJson();
        var signature = GenerateTestSignature(payload, WebhookWebApplicationFactory.TestWebhookSecret);
        var content = new StringContent(payload, Encoding.UTF8, "application/json");
        content.Headers.Add("Stripe-Signature", signature);

        var response = await _client.PostAsync("/webhooks/stripe", content);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private static string BuildMinimalEventJson()
    {
        var epoch = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var apiVersion = StripeConfiguration.ApiVersion ?? "2024-12-18.acacia";
        return $$"""
        {
            "id": "evt_test_valid",
            "object": "event",
            "type": "some.unhandled.event",
            "created": {{epoch}},
            "livemode": false,
            "pending_webhooks": 0,
            "api_version": "{{apiVersion}}",
            "request": { "id": null, "idempotency_key": null },
            "data": {
                "object": {}
            }
        }
        """;
    }

    private static string GenerateTestSignature(string payload, string secret)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var signedPayload = $"{timestamp}.{payload}";
        using var hmac = new System.Security.Cryptography.HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(signedPayload));
        var signature = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        return $"t={timestamp},v1={signature}";
    }
}
