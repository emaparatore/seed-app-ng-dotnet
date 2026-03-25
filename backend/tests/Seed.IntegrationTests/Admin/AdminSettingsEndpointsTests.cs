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

public class AdminSettingsEndpointsTests(CustomWebApplicationFactory factory)
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
    public async Task GetSettings_Without_Auth_Returns_Unauthorized()
    {
        var response = await _client.GetAsync("/api/v1.0/admin/settings");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetSettings_Without_Permission_Returns_Forbidden()
    {
        var userToken = await CreateUserWithRoleAndGetTokenAsync(
            "settings-noperm-get@example.com", SystemRoles.User);
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", userToken);

        var response = await _client.GetAsync("/api/v1.0/admin/settings");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UpdateSettings_Without_Permission_Returns_Forbidden()
    {
        var userToken = await CreateUserWithRoleAndGetTokenAsync(
            "settings-noperm-put@example.com", SystemRoles.User);
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", userToken);

        var response = await _client.PutAsJsonAsync("/api/v1.0/admin/settings", new
        {
            items = new[] { new { key = "General.AppName", value = "Test" } }
        });
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // --- Functional Tests ---

    [Fact]
    public async Task GetSettings_With_Admin_Returns_All_Defaults()
    {
        var adminToken = await CreateUserWithRoleAndGetTokenAsync(
            "settings-get@example.com", SystemRoles.Admin);
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", adminToken);

        var response = await _client.GetAsync("/api/v1.0/admin/settings");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var settings = await response.Content.ReadFromJsonAsync<List<SystemSettingDto>>();
        settings.Should().NotBeNull();
        settings!.Count.Should().BeGreaterThanOrEqualTo(8);
        settings.Should().Contain(s => s.Key == "Security.MaxLoginAttempts");
        settings.Should().Contain(s => s.Key == "General.AppName");
    }

    [Fact]
    public async Task UpdateSettings_With_SuperAdmin_Updates_Values()
    {
        var adminToken = await CreateUserWithRoleAndGetTokenAsync(
            "settings-update@example.com", SystemRoles.SuperAdmin);
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", adminToken);

        var response = await _client.PutAsJsonAsync("/api/v1.0/admin/settings", new
        {
            items = new[] { new { key = "General.AppName", value = "Updated App" } }
        });
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify the update
        var getResponse = await _client.GetAsync("/api/v1.0/admin/settings");
        var settings = await getResponse.Content.ReadFromJsonAsync<List<SystemSettingDto>>();
        settings.Should().Contain(s => s.Key == "General.AppName" && s.Value == "Updated App");
    }

    [Fact]
    public async Task UpdateSettings_Admin_Without_Manage_Permission_Returns_Forbidden()
    {
        // Admin role does not have Settings.Manage permission
        var adminToken = await CreateUserWithRoleAndGetTokenAsync(
            "settings-admin-nomanage@example.com", SystemRoles.Admin);
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", adminToken);

        var response = await _client.PutAsJsonAsync("/api/v1.0/admin/settings", new
        {
            items = new[] { new { key = "General.AppName", value = "Hacked" } }
        });
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // --- DTOs ---

    private record AuthResponseDto(
        string AccessToken, string RefreshToken, DateTime ExpiresAt,
        UserResponseDto User, List<string>? Permissions = null, bool MustChangePassword = false);

    private record UserResponseDto(
        Guid Id, string Email, string FirstName, string LastName, List<string>? Roles = null);

    private record SystemSettingDto(
        string Key, string Value, string Type, string Category,
        string? Description, Guid? ModifiedBy, DateTime? ModifiedAt);
}
