using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Seed.IntegrationTests.Infrastructure;
using Seed.IntegrationTests.Webhooks;

namespace Seed.IntegrationTests.Billing;

public class PaymentsModuleDisabledTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task ModuleDisabled_PlansEndpoint_Returns404()
    {
        var response = await _client.GetAsync("/api/v1.0/plans");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ModuleDisabled_BillingEndpoints_Return404()
    {
        var response = await _client.GetAsync("/api/v1.0/billing/subscription");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ModuleDisabled_WebhookEndpoint_Returns404()
    {
        var response = await _client.PostAsync("/webhooks/stripe", null);
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ModuleDisabled_ConfigEndpoint_ReturnsPaymentsDisabled()
    {
        var response = await _client.GetAsync("/api/v1.0/config");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var config = await response.Content.ReadFromJsonAsync<ConfigResponse>();
        config.Should().NotBeNull();
        config!.PaymentsEnabled.Should().BeFalse();
    }
}

public class PaymentsModuleEnabledConfigTests(WebhookWebApplicationFactory factory)
    : IClassFixture<WebhookWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task ModuleEnabled_ConfigEndpoint_ReturnsPaymentsEnabled()
    {
        var response = await _client.GetAsync("/api/v1.0/config");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var config = await response.Content.ReadFromJsonAsync<ConfigResponse>();
        config.Should().NotBeNull();
        config!.PaymentsEnabled.Should().BeTrue();
    }
}

file record ConfigResponse(bool PaymentsEnabled);
