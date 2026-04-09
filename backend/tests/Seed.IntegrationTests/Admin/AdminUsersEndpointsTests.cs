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

public class AdminUsersEndpointsTests(CustomWebApplicationFactory factory)
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

    private async Task<Guid> CreateTestUserAsync(string email)
    {
        await _client.PostAsJsonAsync("/api/v1.0/auth/register", new
        {
            email,
            password = "Password1",
            firstName = "Target",
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
        return user2!.Id;
    }

    // --- Authorization Tests ---

    [Fact]
    public async Task GetUsers_Without_Auth_Returns_Unauthorized()
    {
        var response = await _client.GetAsync("/api/v1.0/admin/users");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetUsers_Without_Permission_Returns_Forbidden()
    {
        // Register a user without any admin role
        await _client.PostAsJsonAsync("/api/v1.0/auth/register", new
        {
            email = "noperm-list@example.com",
            password = "Password1",
            firstName = "No",
            lastName = "Perm",
            acceptPrivacyPolicy = true,
            acceptTermsOfService = true
        });

        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync("noperm-list@example.com");
        var token = await userManager.GenerateEmailConfirmationTokenAsync(user!);
        await _client.PostAsJsonAsync("/api/v1.0/auth/confirm-email",
            new { email = "noperm-list@example.com", token });

        var loginResponse = await _client.PostAsJsonAsync("/api/v1.0/auth/login", new
        {
            email = "noperm-list@example.com",
            password = "Password1"
        });
        var auth = await loginResponse.Content.ReadFromJsonAsync<AuthResponseDto>();

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", auth!.AccessToken);

        var response = await _client.GetAsync("/api/v1.0/admin/users");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetUsers_With_Admin_Role_Returns_Ok()
    {
        var adminToken = await CreateUserWithRoleAndGetTokenAsync(
            "admin-list@example.com", SystemRoles.Admin);
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", adminToken);

        var response = await _client.GetAsync("/api/v1.0/admin/users");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<PagedResultDto>();
        body.Should().NotBeNull();
        body!.Items.Should().NotBeEmpty();
        body.TotalCount.Should().BeGreaterThan(0);
    }

    // --- CRUD Tests ---

    [Fact]
    public async Task CreateUser_With_Admin_Returns_Created()
    {
        var adminToken = await CreateUserWithRoleAndGetTokenAsync(
            "admin-create@example.com", SystemRoles.Admin);
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", adminToken);

        var response = await _client.PostAsJsonAsync("/api/v1.0/admin/users", new
        {
            email = "newuser-admin@example.com",
            firstName = "New",
            lastName = "User",
            password = "Password1",
            roleNames = new[] { "User" }
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task GetUserById_Returns_User_Detail()
    {
        var adminToken = await CreateUserWithRoleAndGetTokenAsync(
            "admin-detail@example.com", SystemRoles.Admin);
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", adminToken);

        var targetUserId = await CreateTestUserAsync("detail-target@example.com");

        var response = await _client.GetAsync($"/api/v1.0/admin/users/{targetUserId}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<AdminUserDetailResponseDto>();
        body.Should().NotBeNull();
        body!.Email.Should().Be("detail-target@example.com");
        body.EmailConfirmed.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateUser_Returns_NoContent()
    {
        var adminToken = await CreateUserWithRoleAndGetTokenAsync(
            "admin-update@example.com", SystemRoles.Admin);
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", adminToken);

        var targetUserId = await CreateTestUserAsync("update-target@example.com");

        var response = await _client.PutAsJsonAsync($"/api/v1.0/admin/users/{targetUserId}", new
        {
            firstName = "Updated",
            lastName = "Name",
            email = "update-target@example.com"
        });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    // --- Protection Tests ---

    [Fact]
    public async Task DeleteUser_Cannot_Delete_SuperAdmin()
    {
        var superAdminToken = await CreateUserWithRoleAndGetTokenAsync(
            "sa-delete-test@example.com", SystemRoles.SuperAdmin);
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", superAdminToken);

        // Create another SuperAdmin to try to delete
        var targetEmail = "sa-target-delete@example.com";
        await CreateTestUserAsync(targetEmail);
        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var targetUser = await userManager.FindByEmailAsync(targetEmail);
        await userManager.AddToRoleAsync(targetUser!, SystemRoles.SuperAdmin);

        var response = await _client.DeleteAsync($"/api/v1.0/admin/users/{targetUser!.Id}");
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ToggleStatus_Cannot_Deactivate_SuperAdmin()
    {
        var superAdminToken = await CreateUserWithRoleAndGetTokenAsync(
            "sa-toggle-test@example.com", SystemRoles.SuperAdmin);
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", superAdminToken);

        var targetEmail = "sa-target-toggle@example.com";
        await CreateTestUserAsync(targetEmail);
        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var targetUser = await userManager.FindByEmailAsync(targetEmail);
        await userManager.AddToRoleAsync(targetUser!, SystemRoles.SuperAdmin);

        var response = await _client.PutAsJsonAsync(
            $"/api/v1.0/admin/users/{targetUser!.Id}/status",
            new { isActive = false });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task DeleteUser_Soft_Deletes_User()
    {
        var adminToken = await CreateUserWithRoleAndGetTokenAsync(
            "admin-softdel@example.com", SystemRoles.SuperAdmin);
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", adminToken);

        var targetUserId = await CreateTestUserAsync("softdel-target@example.com");

        var response = await _client.DeleteAsync($"/api/v1.0/admin/users/{targetUserId}");
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify user is soft-deleted: not visible via API
        var getResponse = await _client.GetAsync($"/api/v1.0/admin/users/{targetUserId}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AssignRoles_Cannot_Assign_SuperAdmin_Role()
    {
        var superAdminToken = await CreateUserWithRoleAndGetTokenAsync(
            "sa-assignrole@example.com", SystemRoles.SuperAdmin);
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", superAdminToken);

        var targetUserId = await CreateTestUserAsync("assignrole-target@example.com");

        var response = await _client.PutAsJsonAsync(
            $"/api/v1.0/admin/users/{targetUserId}/roles",
            new { roleNames = new[] { SystemRoles.SuperAdmin } });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // --- Pagination Tests ---

    [Fact]
    public async Task GetUsers_Supports_Pagination()
    {
        var adminToken = await CreateUserWithRoleAndGetTokenAsync(
            "admin-page@example.com", SystemRoles.Admin);
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", adminToken);

        var response = await _client.GetAsync("/api/v1.0/admin/users?pageNumber=1&pageSize=2");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<PagedResultDto>();
        body.Should().NotBeNull();
        body!.PageNumber.Should().Be(1);
        body.PageSize.Should().Be(2);
        body.Items.Count.Should().BeLessThanOrEqualTo(2);
    }

    [Fact]
    public async Task GetUsers_Supports_Search()
    {
        var adminToken = await CreateUserWithRoleAndGetTokenAsync(
            "admin-search@example.com", SystemRoles.Admin);
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", adminToken);

        await CreateTestUserAsync("searchable-unique@example.com");

        var response = await _client.GetAsync(
            "/api/v1.0/admin/users?searchTerm=searchable-unique");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<PagedResultDto>();
        body!.Items.Should().ContainSingle(u => u.Email == "searchable-unique@example.com");
    }

    // --- DTOs ---

    private record AuthResponseDto(
        string AccessToken, string RefreshToken, DateTime ExpiresAt,
        UserResponseDto User, List<string>? Permissions = null, bool MustChangePassword = false);

    private record UserResponseDto(
        Guid Id, string Email, string FirstName, string LastName, List<string>? Roles = null);

    private record PagedResultDto(
        List<AdminUserItemDto> Items, int PageNumber, int PageSize,
        int TotalCount, int TotalPages, bool HasPreviousPage, bool HasNextPage);

    private record AdminUserItemDto(
        Guid Id, string Email, string FirstName, string LastName,
        bool IsActive, List<string> Roles, DateTime CreatedAt);

    private record AdminUserDetailResponseDto(
        Guid Id, string Email, string FirstName, string LastName,
        bool IsActive, List<string> Roles, DateTime CreatedAt,
        DateTime UpdatedAt, bool MustChangePassword, bool EmailConfirmed);
}
