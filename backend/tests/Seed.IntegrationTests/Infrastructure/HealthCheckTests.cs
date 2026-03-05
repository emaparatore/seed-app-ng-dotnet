using System.Net;
using FluentAssertions;
using Seed.IntegrationTests.Infrastructure;

namespace Seed.IntegrationTests.Infrastructure;

public class HealthCheckTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Health_Endpoint_Returns_Healthy()
    {
        var response = await _client.GetAsync("/health");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Health_Live_Returns_Healthy()
    {
        var response = await _client.GetAsync("/health/live");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Health_Ready_Returns_Healthy()
    {
        var response = await _client.GetAsync("/health/ready");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
