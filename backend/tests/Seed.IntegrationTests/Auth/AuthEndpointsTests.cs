using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Seed.Domain.Entities;
using Seed.IntegrationTests.Infrastructure;

namespace Seed.IntegrationTests.Auth;

public class AuthEndpointsTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    /// <summary>
    /// Registers a user and confirms their email, returning auth tokens.
    /// Uses UserManager directly to generate the confirmation token without needing SMTP.
    /// </summary>
    private async Task<AuthResponseDto> RegisterAndConfirmUserAsync(
        string email = "test@example.com",
        string password = "Password1",
        string firstName = "John",
        string lastName = "Doe")
    {
        var regResponse = await _client.PostAsJsonAsync("/api/v1.0/auth/register", new
        {
            email, password, firstName, lastName, acceptPrivacyPolicy = true, acceptTermsOfService = true
        });
        regResponse.EnsureSuccessStatusCode();

        // Generate the confirmation token directly via UserManager (no SMTP needed)
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
        return (await confirmResponse.Content.ReadFromJsonAsync<AuthResponseDto>())!;
    }

    [Fact]
    public async Task Register_With_Valid_Data_Returns_Ok_With_Message()
    {
        var response = await _client.PostAsJsonAsync("/api/v1.0/auth/register", new
        {
            email = "register-ok@example.com",
            password = "Password1",
            firstName = "John",
            lastName = "Doe",
            acceptPrivacyPolicy = true,
            acceptTermsOfService = true
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<MessageResponseDto>();
        body.Should().NotBeNull();
        body!.Message.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ConfirmEmail_With_Valid_Token_Returns_AuthTokens()
    {
        var auth = await RegisterAndConfirmUserAsync("confirm-ok@example.com");

        auth.Should().NotBeNull();
        auth.AccessToken.Should().NotBeNullOrEmpty();
        auth.RefreshToken.Should().NotBeNullOrEmpty();
        auth.User.Email.Should().Be("confirm-ok@example.com");
    }

    [Fact]
    public async Task ConfirmEmail_With_Invalid_Token_Returns_BadRequest()
    {
        await _client.PostAsJsonAsync("/api/v1.0/auth/register", new
        {
            email = "confirm-bad@example.com",
            password = "Password1",
            firstName = "John",
            lastName = "Doe",
            acceptPrivacyPolicy = true,
            acceptTermsOfService = true
        });

        var response = await _client.PostAsJsonAsync("/api/v1.0/auth/confirm-email", new
        {
            email = "confirm-bad@example.com",
            token = "bad-token"
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Register_With_Invalid_Data_Returns_BadRequest()
    {
        var response = await _client.PostAsJsonAsync("/api/v1.0/auth/register", new
        {
            email = "", password = "", firstName = "", lastName = "", acceptPrivacyPolicy = false, acceptTermsOfService = false
        });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Register_With_Duplicate_Email_Returns_BadRequest()
    {
        await RegisterAndConfirmUserAsync("duplicate@example.com");

        var response = await _client.PostAsJsonAsync("/api/v1.0/auth/register", new
        {
            email = "duplicate@example.com",
            password = "Password1",
            firstName = "Jane",
            lastName = "Doe",
            acceptPrivacyPolicy = true,
            acceptTermsOfService = true
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Login_With_Unverified_Email_Returns_Unauthorized()
    {
        await _client.PostAsJsonAsync("/api/v1.0/auth/register", new
        {
            email = "unverified@example.com",
            password = "Password1",
            firstName = "John",
            lastName = "Doe",
            acceptPrivacyPolicy = true,
            acceptTermsOfService = true
        });

        var response = await _client.PostAsJsonAsync("/api/v1.0/auth/login", new
        {
            email = "unverified@example.com",
            password = "Password1"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_With_Valid_Credentials_Returns_Ok()
    {
        await RegisterAndConfirmUserAsync("login-ok@example.com");

        var response = await _client.PostAsJsonAsync("/api/v1.0/auth/login", new
        {
            email = "login-ok@example.com",
            password = "Password1"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<AuthResponseDto>();
        body!.AccessToken.Should().NotBeNullOrEmpty();
        body.User.Email.Should().Be("login-ok@example.com");
    }

    [Fact]
    public async Task Login_With_Wrong_Password_Returns_Unauthorized()
    {
        await RegisterAndConfirmUserAsync("login-wrong@example.com");

        var response = await _client.PostAsJsonAsync("/api/v1.0/auth/login", new
        {
            email = "login-wrong@example.com",
            password = "WrongPass1"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_With_Valid_Token_Returns_New_Tokens()
    {
        var auth = await RegisterAndConfirmUserAsync("refresh-ok@example.com");

        var response = await _client.PostAsJsonAsync("/api/v1.0/auth/refresh", new
        {
            refreshToken = auth.RefreshToken
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<AuthResponseDto>();
        body!.AccessToken.Should().NotBeNullOrEmpty();
        body.AccessToken.Should().NotBe(auth.AccessToken);
    }

    [Fact]
    public async Task Refresh_With_Invalid_Token_Returns_Unauthorized()
    {
        var response = await _client.PostAsJsonAsync("/api/v1.0/auth/refresh", new
        {
            refreshToken = "invalid-token"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Logout_Without_Auth_Returns_Unauthorized()
    {
        var response = await _client.PostAsJsonAsync("/api/v1.0/auth/logout", new
        {
            refreshToken = "some-token"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Logout_With_Auth_Returns_NoContent()
    {
        var auth = await RegisterAndConfirmUserAsync("logout-ok@example.com");
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        var response = await _client.PostAsJsonAsync("/api/v1.0/auth/logout", new
        {
            refreshToken = auth.RefreshToken
        });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Me_Without_Auth_Returns_Unauthorized()
    {
        var response = await _client.GetAsync("/api/v1.0/auth/me");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Me_With_Auth_Returns_User_Data()
    {
        var auth = await RegisterAndConfirmUserAsync("me-ok@example.com");
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        var response = await _client.GetAsync("/api/v1.0/auth/me");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<UserResponseDto>();
        body!.Email.Should().Be("me-ok@example.com");
        body.FirstName.Should().Be("John");
    }

    private record AuthResponseDto(string AccessToken, string RefreshToken, DateTime ExpiresAt, UserResponseDto User, List<string>? Permissions = null, bool MustChangePassword = false);
    private record UserResponseDto(Guid Id, string Email, string FirstName, string LastName, List<string>? Roles = null);
    private record MessageResponseDto(string Message);
}
