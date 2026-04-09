using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Seed.Domain.Authorization;
using Seed.Domain.Entities;
using Seed.IntegrationTests.Infrastructure;

namespace Seed.IntegrationTests.Authorization;

public class PermissionAuthorizationTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private async Task<string> CreateUserAndGetTokenAsync(
        string email,
        string password = "Password1",
        string? roleName = null)
    {
        // Register
        await _client.PostAsJsonAsync("/api/v1.0/auth/register", new
        {
            email,
            password,
            firstName = "Test",
            lastName = "User",
            acceptPrivacyPolicy = true,
            acceptTermsOfService = true
        });

        // Confirm email
        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync(email);
        var token = await userManager.GenerateEmailConfirmationTokenAsync(user!);

        var confirmResponse = await _client.PostAsJsonAsync("/api/v1.0/auth/confirm-email", new
        {
            email,
            token
        });
        confirmResponse.EnsureSuccessStatusCode();

        // Assign role if specified
        if (roleName is not null)
        {
            // Re-fetch user in a new scope to avoid tracking issues
            using var scope2 = factory.Services.CreateScope();
            var userManager2 = scope2.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user2 = await userManager2.FindByEmailAsync(email);
            await userManager2.AddToRoleAsync(user2!, roleName);
        }

        // Login to get fresh token with role claims
        var loginResponse = await _client.PostAsJsonAsync("/api/v1.0/auth/login", new
        {
            email,
            password
        });
        loginResponse.EnsureSuccessStatusCode();

        var auth = await loginResponse.Content.ReadFromJsonAsync<AuthResponseDto>();
        return auth!.AccessToken;
    }

    [Fact]
    public async Task Login_Response_Should_Include_Permissions_And_Roles()
    {
        var token = await CreateUserAndGetTokenAsync(
            "perms-check@example.com",
            roleName: SystemRoles.Admin);

        // Login again to verify the response shape
        var loginResponse = await _client.PostAsJsonAsync("/api/v1.0/auth/login", new
        {
            email = "perms-check@example.com",
            password = "Password1"
        });
        loginResponse.EnsureSuccessStatusCode();

        var auth = await loginResponse.Content.ReadFromJsonAsync<AuthResponseDto>();
        auth!.User.Roles.Should().Contain(SystemRoles.Admin);
        auth.Permissions.Should().NotBeEmpty();
        auth.Permissions.Should().Contain(Permissions.Users.Read);
        // Admin should NOT have Settings.Manage
        auth.Permissions.Should().NotContain(Permissions.Settings.Manage);
    }

    [Fact]
    public async Task Login_Response_Should_Include_MustChangePassword_Flag()
    {
        // Register and confirm a user
        await _client.PostAsJsonAsync("/api/v1.0/auth/register", new
        {
            email = "must-change@example.com",
            password = "Password1",
            firstName = "Test",
            lastName = "User",
            acceptPrivacyPolicy = true,
            acceptTermsOfService = true
        });

        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync("must-change@example.com");
        var token = await userManager.GenerateEmailConfirmationTokenAsync(user!);

        await _client.PostAsJsonAsync("/api/v1.0/auth/confirm-email", new
        {
            email = "must-change@example.com",
            token
        });

        // Set MustChangePassword flag directly
        using var scope2 = factory.Services.CreateScope();
        var userManager2 = scope2.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user2 = await userManager2.FindByEmailAsync("must-change@example.com");
        user2!.MustChangePassword = true;
        await userManager2.UpdateAsync(user2);

        // Login and verify
        var loginResponse = await _client.PostAsJsonAsync("/api/v1.0/auth/login", new
        {
            email = "must-change@example.com",
            password = "Password1"
        });
        loginResponse.EnsureSuccessStatusCode();

        var auth = await loginResponse.Content.ReadFromJsonAsync<AuthResponseDto>();
        auth!.MustChangePassword.Should().BeTrue();
    }

    [Fact]
    public async Task SuperAdmin_Login_Should_Have_All_Permissions()
    {
        var token = await CreateUserAndGetTokenAsync(
            "superadmin-perms@example.com",
            roleName: SystemRoles.SuperAdmin);

        var loginResponse = await _client.PostAsJsonAsync("/api/v1.0/auth/login", new
        {
            email = "superadmin-perms@example.com",
            password = "Password1"
        });
        loginResponse.EnsureSuccessStatusCode();

        var auth = await loginResponse.Content.ReadFromJsonAsync<AuthResponseDto>();
        var allPermissions = Permissions.GetAll();

        auth!.Permissions.Should().HaveCount(allPermissions.Count);
        foreach (var perm in allPermissions)
        {
            auth.Permissions.Should().Contain(perm);
        }
    }

    [Fact]
    public async Task User_Without_Roles_Should_Have_No_Permissions()
    {
        var loginResponse = await RegisterConfirmAndLoginAsync("no-perms@example.com");

        var auth = await loginResponse.Content.ReadFromJsonAsync<AuthResponseDto>();
        auth!.Permissions.Should().BeEmpty();
    }

    private async Task<HttpResponseMessage> RegisterConfirmAndLoginAsync(string email, string password = "Password1")
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

        await _client.PostAsJsonAsync("/api/v1.0/auth/confirm-email", new
        {
            email,
            token
        });

        return await _client.PostAsJsonAsync("/api/v1.0/auth/login", new
        {
            email,
            password
        });
    }

    private record AuthResponseDto(
        string AccessToken,
        string RefreshToken,
        DateTime ExpiresAt,
        UserResponseDto User,
        List<string> Permissions,
        bool MustChangePassword);

    private record UserResponseDto(
        Guid Id,
        string Email,
        string FirstName,
        string LastName,
        List<string> Roles);
}
