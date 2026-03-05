using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Seed.IntegrationTests.Infrastructure;

namespace Seed.IntegrationTests.Home;

public class HomeEndpointsTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Get_Home_Returns_Ok_With_Greeting()
    {
        var response = await _client.GetAsync("/api/v1.0/home");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<GreetingResponse>();
        body!.Message.Should().Be("Hello, Seed!");
    }

    private record GreetingResponse(string Message);
}
