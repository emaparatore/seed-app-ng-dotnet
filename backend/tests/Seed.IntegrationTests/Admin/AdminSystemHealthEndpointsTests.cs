using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Seed.Domain.Authorization;
using Seed.Domain.Entities;
using Seed.IntegrationTests.Infrastructure;

namespace Seed.IntegrationTests.Admin;

public class AdminSystemHealthEndpointsTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private async Task<string> CreateUserWithRoleAndGetTokenAsync(
        string email,
        string roleName,
        string password = "Password1")
    {
        await _client.PostAsJsonAsync("/api/v1.0/auth/register", new
        {
            email,
            password,
            firstName = "Test",
            lastName = "User"
        });

        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync(email);
        var token = await userManager.GenerateEmailConfirmationTokenAsync(user!);

        await _client.PostAsJsonAsync("/api/v1.0/auth/confirm-email", new { email, token });

        using var scope2 = factory.Services.CreateScope();
        var userManager2 = scope2.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user2 = await userManager2.FindByEmailAsync(email);
        await userManager2.AddToRoleAsync(user2!, roleName);

        var loginResponse = await _client.PostAsJsonAsync("/api/v1.0/auth/login", new { email, password });
        loginResponse.EnsureSuccessStatusCode();

        var auth = await loginResponse.Content.ReadFromJsonAsync<AuthResponseDto>();
        return auth!.AccessToken;
    }

    // --- Authorization Tests ---

    [Fact]
    public async Task GetSystemHealth_WhenNotAuthenticated_Returns401()
    {
        var response = await _client.GetAsync("/api/v1.0/admin/system-health");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetSystemHealth_WhenNoPermission_Returns403()
    {
        var userToken = await CreateUserWithRoleAndGetTokenAsync(
            "health-noperm@example.com", SystemRoles.User);
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", userToken);

        var response = await _client.GetAsync("/api/v1.0/admin/system-health");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetSystemHealth_WhenSuperAdmin_ReturnsCompleteResponse()
    {
        var adminToken = await CreateUserWithRoleAndGetTokenAsync(
            "health-super@example.com", SystemRoles.SuperAdmin);
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", adminToken);

        var response = await _client.GetAsync("/api/v1.0/admin/system-health");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var health = await response.Content.ReadFromJsonAsync<SystemHealthResponseDto>();
        health.Should().NotBeNull();
        health!.Database.Should().NotBeNull();
        health.Email.Should().NotBeNull();
        health.Version.Should().NotBeNullOrEmpty();
        health.Environment.Should().NotBeNullOrEmpty();
        health.Uptime.Should().NotBeNull();
        health.Memory.Should().NotBeNull();
    }

    [Fact]
    public async Task GetSystemHealth_ResponseContainsValidDatabaseStatus()
    {
        var adminToken = await CreateUserWithRoleAndGetTokenAsync(
            "health-db@example.com", SystemRoles.SuperAdmin);
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", adminToken);

        var response = await _client.GetAsync("/api/v1.0/admin/system-health");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var health = await response.Content.ReadFromJsonAsync<SystemHealthResponseDto>();
        health.Should().NotBeNull();
        health!.Database.Status.Should().BeOneOf("Healthy", "Degraded", "Unhealthy");
    }

    [Fact]
    public async Task GetSystemHealth_ResponseContainsVersionAndEnvironment()
    {
        var adminToken = await CreateUserWithRoleAndGetTokenAsync(
            "health-ver@example.com", SystemRoles.SuperAdmin);
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", adminToken);

        var response = await _client.GetAsync("/api/v1.0/admin/system-health");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var health = await response.Content.ReadFromJsonAsync<SystemHealthResponseDto>();
        health.Should().NotBeNull();
        health!.Version.Should().NotBeNullOrEmpty();
        health.Environment.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GetSystemHealth_ResponseContainsPositiveUptimeAndMemory()
    {
        var adminToken = await CreateUserWithRoleAndGetTokenAsync(
            "health-mem@example.com", SystemRoles.SuperAdmin);
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", adminToken);

        var response = await _client.GetAsync("/api/v1.0/admin/system-health");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var health = await response.Content.ReadFromJsonAsync<SystemHealthResponseDto>();
        health.Should().NotBeNull();
        health!.Uptime.TotalSeconds.Should().BeGreaterThan(0);
        health.Uptime.Formatted.Should().NotBeNullOrEmpty();
        health.Memory.WorkingSetMegabytes.Should().BeGreaterThan(0);
        health.Memory.GcAllocatedMegabytes.Should().BeGreaterThan(0);
    }

    // --- DTOs ---

    private record AuthResponseDto(
        string AccessToken, string RefreshToken, DateTime ExpiresAt,
        UserResponseDto User, List<string>? Permissions = null, bool MustChangePassword = false);

    private record UserResponseDto(
        Guid Id, string Email, string FirstName, string LastName, List<string>? Roles = null);

    private record SystemHealthResponseDto(
        ComponentStatusResponseDto Database,
        ComponentStatusResponseDto Email,
        string Version,
        string Environment,
        UptimeResponseDto Uptime,
        MemoryResponseDto Memory);

    private record ComponentStatusResponseDto(string Status, string? Description);

    private record UptimeResponseDto(long TotalSeconds, string Formatted);

    private record MemoryResponseDto(double WorkingSetMegabytes, double GcAllocatedMegabytes);
}
