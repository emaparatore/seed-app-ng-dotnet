using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Seed.Domain.Entities;
using Seed.IntegrationTests.Infrastructure;

namespace Seed.IntegrationTests.Auth;

public class ChangePasswordTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private async Task<AuthResponseDto> RegisterAndConfirmUserAsync(
        string email,
        string password = "Password1",
        string firstName = "John",
        string lastName = "Doe")
    {
        var regResponse = await _client.PostAsJsonAsync("/api/v1.0/auth/register", new
        {
            email, password, firstName, lastName, acceptPrivacyPolicy = true, acceptTermsOfService = true
        });
        regResponse.EnsureSuccessStatusCode();

        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync(email);
        var token = await userManager.GenerateEmailConfirmationTokenAsync(user!);

        var confirmResponse = await _client.PostAsJsonAsync("/api/v1.0/auth/confirm-email", new
        {
            email, token
        });
        confirmResponse.EnsureSuccessStatusCode();
        return (await confirmResponse.Content.ReadFromJsonAsync<AuthResponseDto>())!;
    }

    private async Task SetMustChangePasswordAsync(string email, bool value = true)
    {
        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync(email);
        user!.MustChangePassword = value;
        await userManager.UpdateAsync(user);
    }

    [Fact]
    public async Task ChangePassword_With_Valid_Data_Returns_Ok()
    {
        var auth = await RegisterAndConfirmUserAsync("cp-ok@example.com");
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        var response = await _client.PostAsJsonAsync("/api/v1.0/auth/change-password", new
        {
            currentPassword = "Password1",
            newPassword = "NewPassword1"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ChangePassword_Clears_MustChangePassword_Flag()
    {
        var email = "cp-flag@example.com";
        var auth = await RegisterAndConfirmUserAsync(email);
        await SetMustChangePasswordAsync(email);

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        var response = await _client.PostAsJsonAsync("/api/v1.0/auth/change-password", new
        {
            currentPassword = "Password1",
            newPassword = "NewPassword1"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify the flag was cleared
        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync(email);
        user!.MustChangePassword.Should().BeFalse();
    }

    [Fact]
    public async Task ChangePassword_With_Wrong_Current_Password_Returns_BadRequest()
    {
        var auth = await RegisterAndConfirmUserAsync("cp-wrong@example.com");
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        var response = await _client.PostAsJsonAsync("/api/v1.0/auth/change-password", new
        {
            currentPassword = "WrongPassword1",
            newPassword = "NewPassword1"
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ChangePassword_Without_Auth_Returns_Unauthorized()
    {
        var response = await _client.PostAsJsonAsync("/api/v1.0/auth/change-password", new
        {
            currentPassword = "Password1",
            newPassword = "NewPassword1"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // Middleware tests

    [Fact]
    public async Task Middleware_Blocks_Generic_Endpoint_When_MustChangePassword_Is_True()
    {
        var email = "mw-block@example.com";
        var auth = await RegisterAndConfirmUserAsync(email);
        await SetMustChangePasswordAsync(email);

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        var response = await _client.GetAsync("/api/v1.0/auth/me");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var body = await response.Content.ReadFromJsonAsync<PasswordChangeRequiredDto>();
        body!.Type.Should().Be("PASSWORD_CHANGE_REQUIRED");
    }

    [Fact]
    public async Task Middleware_Allows_ChangePassword_Endpoint_When_MustChangePassword_Is_True()
    {
        var email = "mw-allow-cp@example.com";
        var auth = await RegisterAndConfirmUserAsync(email);
        await SetMustChangePasswordAsync(email);

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        var response = await _client.PostAsJsonAsync("/api/v1.0/auth/change-password", new
        {
            currentPassword = "Password1",
            newPassword = "NewPassword1"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Middleware_Allows_Logout_When_MustChangePassword_Is_True()
    {
        var email = "mw-allow-logout@example.com";
        var auth = await RegisterAndConfirmUserAsync(email);
        await SetMustChangePasswordAsync(email);

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        var response = await _client.PostAsJsonAsync("/api/v1.0/auth/logout", new
        {
            refreshToken = auth.RefreshToken
        });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Middleware_Allows_Request_When_MustChangePassword_Is_False()
    {
        var auth = await RegisterAndConfirmUserAsync("mw-pass@example.com");
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        var response = await _client.GetAsync("/api/v1.0/auth/me");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private record AuthResponseDto(string AccessToken, string RefreshToken, DateTime ExpiresAt, UserResponseDto User);
    private record UserResponseDto(Guid Id, string Email, string FirstName, string LastName);
    private record PasswordChangeRequiredDto(string Type, string Title, int Status);
}
