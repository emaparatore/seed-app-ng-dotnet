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

public class AdminDashboardEndpointsTests(CustomWebApplicationFactory factory)
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
            lastName = "User",
            acceptPrivacyPolicy = true,
            acceptTermsOfService = true
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
    public async Task GetDashboard_WithoutAuth_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/api/v1.0/admin/dashboard");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetDashboard_WithoutPermission_ReturnsForbidden()
    {
        var userToken = await CreateUserWithRoleAndGetTokenAsync(
            "dashboard-noperm@example.com", SystemRoles.User);
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", userToken);

        var response = await _client.GetAsync("/api/v1.0/admin/dashboard");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetDashboard_WithPermission_ReturnsOk()
    {
        var adminToken = await CreateUserWithRoleAndGetTokenAsync(
            "dashboard-ok@example.com", SystemRoles.Admin);
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", adminToken);

        var response = await _client.GetAsync("/api/v1.0/admin/dashboard");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetDashboard_ReturnsCorrectStructure()
    {
        var adminToken = await CreateUserWithRoleAndGetTokenAsync(
            "dashboard-structure@example.com", SystemRoles.Admin);
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", adminToken);

        var response = await _client.GetAsync("/api/v1.0/admin/dashboard");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var stats = await response.Content.ReadFromJsonAsync<DashboardStatsResponseDto>();
        stats.Should().NotBeNull();
        stats!.TotalUsers.Should().BeGreaterThanOrEqualTo(0);
        stats.ActiveUsers.Should().BeGreaterThanOrEqualTo(0);
        stats.InactiveUsers.Should().BeGreaterThanOrEqualTo(0);
        stats.RegistrationsLast7Days.Should().BeGreaterThanOrEqualTo(0);
        stats.RegistrationsLast30Days.Should().BeGreaterThanOrEqualTo(0);
        stats.RegistrationTrend.Should().NotBeNull();
        stats.UsersByRole.Should().NotBeNull();
        stats.RecentActivity.Should().NotBeNull();
    }

    [Fact]
    public async Task GetDashboard_ReturnsConsistentCounts()
    {
        var adminToken = await CreateUserWithRoleAndGetTokenAsync(
            "dashboard-consistent@example.com", SystemRoles.Admin);
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", adminToken);

        var response = await _client.GetAsync("/api/v1.0/admin/dashboard");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var stats = await response.Content.ReadFromJsonAsync<DashboardStatsResponseDto>();
        stats.Should().NotBeNull();
        stats!.TotalUsers.Should().Be(stats.ActiveUsers + stats.InactiveUsers);
    }

    [Fact]
    public async Task GetDashboard_ReturnsRegistrationTrend()
    {
        var adminToken = await CreateUserWithRoleAndGetTokenAsync(
            "dashboard-trend@example.com", SystemRoles.Admin);
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", adminToken);

        var response = await _client.GetAsync("/api/v1.0/admin/dashboard");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var stats = await response.Content.ReadFromJsonAsync<DashboardStatsResponseDto>();
        stats.Should().NotBeNull();
        stats!.RegistrationTrend.Should().HaveCount(30);

        // Verify dates are ordered chronologically
        for (var i = 1; i < stats.RegistrationTrend.Count; i++)
        {
            string.Compare(stats.RegistrationTrend[i].Date,
                stats.RegistrationTrend[i - 1].Date, StringComparison.Ordinal)
                .Should().BePositive();
        }
    }

    // --- DTOs ---

    private record AuthResponseDto(
        string AccessToken, string RefreshToken, DateTime ExpiresAt,
        UserResponseDto User, List<string>? Permissions = null, bool MustChangePassword = false);

    private record UserResponseDto(
        Guid Id, string Email, string FirstName, string LastName, List<string>? Roles = null);

    private record DashboardStatsResponseDto(
        int TotalUsers,
        int ActiveUsers,
        int InactiveUsers,
        int RegistrationsLast7Days,
        int RegistrationsLast30Days,
        List<DailyRegistrationResponseDto> RegistrationTrend,
        List<RoleDistributionResponseDto> UsersByRole,
        List<RecentActivityResponseDto> RecentActivity);

    private record DailyRegistrationResponseDto(string Date, int Count);

    private record RoleDistributionResponseDto(string RoleName, int UserCount);

    private record RecentActivityResponseDto(
        Guid Id, DateTime Timestamp, string Action, string EntityType, Guid? UserId);
}
