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

public class AdminRolesEndpointsTests(CustomWebApplicationFactory factory)
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
    public async Task GetRoles_Without_Auth_Returns_Unauthorized()
    {
        var response = await _client.GetAsync("/api/v1.0/admin/roles");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetRoles_Without_Permission_Returns_Forbidden()
    {
        await _client.PostAsJsonAsync("/api/v1.0/auth/register", new
        {
            email = "noperm-roles@example.com",
            password = "Password1",
            firstName = "No",
            lastName = "Perm"
        });

        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync("noperm-roles@example.com");
        var token = await userManager.GenerateEmailConfirmationTokenAsync(user!);
        await _client.PostAsJsonAsync("/api/v1.0/auth/confirm-email",
            new { email = "noperm-roles@example.com", token });

        var loginResponse = await _client.PostAsJsonAsync("/api/v1.0/auth/login", new
        {
            email = "noperm-roles@example.com",
            password = "Password1"
        });
        var auth = await loginResponse.Content.ReadFromJsonAsync<AuthResponseDto>();

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", auth!.AccessToken);

        var response = await _client.GetAsync("/api/v1.0/admin/roles");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetRoles_With_Admin_Role_Returns_Ok()
    {
        var adminToken = await CreateUserWithRoleAndGetTokenAsync(
            "admin-roles-list@example.com", SystemRoles.Admin);
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", adminToken);

        var response = await _client.GetAsync("/api/v1.0/admin/roles");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<List<RoleItemDto>>();
        body.Should().NotBeNull();
        body!.Should().NotBeEmpty();
        body.Should().Contain(r => r.Name == SystemRoles.SuperAdmin);
        body.Should().Contain(r => r.Name == SystemRoles.Admin);
        body.Should().Contain(r => r.Name == SystemRoles.User);
    }

    // --- CRUD Tests ---

    [Fact]
    public async Task CreateRole_With_Admin_Returns_Created()
    {
        var adminToken = await CreateUserWithRoleAndGetTokenAsync(
            "admin-roles-create@example.com", SystemRoles.Admin);
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", adminToken);

        var response = await _client.PostAsJsonAsync("/api/v1.0/admin/roles", new
        {
            name = "TestEditor",
            description = "Test editor role",
            permissionNames = new[] { "Users.Read" }
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task GetRoleById_Returns_Role_Detail()
    {
        var adminToken = await CreateUserWithRoleAndGetTokenAsync(
            "admin-roles-detail@example.com", SystemRoles.Admin);
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", adminToken);

        // Create a role first
        var createResponse = await _client.PostAsJsonAsync("/api/v1.0/admin/roles", new
        {
            name = "DetailTestRole",
            description = "For detail test",
            permissionNames = new[] { "Users.Read", "Users.Create" }
        });
        var created = await createResponse.Content.ReadFromJsonAsync<CreatedResponseDto>();

        var response = await _client.GetAsync($"/api/v1.0/admin/roles/{created!.Id}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<RoleDetailDto>();
        body.Should().NotBeNull();
        body!.Name.Should().Be("DetailTestRole");
        body.Permissions.Should().Contain("Users.Read");
        body.Permissions.Should().Contain("Users.Create");
    }

    [Fact]
    public async Task UpdateRole_Returns_NoContent()
    {
        var adminToken = await CreateUserWithRoleAndGetTokenAsync(
            "admin-roles-update@example.com", SystemRoles.Admin);
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", adminToken);

        var createResponse = await _client.PostAsJsonAsync("/api/v1.0/admin/roles", new
        {
            name = "UpdateTestRole",
            description = "Before update",
            permissionNames = new[] { "Users.Read" }
        });
        var created = await createResponse.Content.ReadFromJsonAsync<CreatedResponseDto>();

        var response = await _client.PutAsJsonAsync($"/api/v1.0/admin/roles/{created!.Id}", new
        {
            name = "UpdatedRole",
            description = "After update",
            permissionNames = new[] { "Users.Read", "Users.Update" }
        });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task DeleteRole_Returns_NoContent()
    {
        var adminToken = await CreateUserWithRoleAndGetTokenAsync(
            "admin-roles-delete@example.com", SystemRoles.SuperAdmin);
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", adminToken);

        var createResponse = await _client.PostAsJsonAsync("/api/v1.0/admin/roles", new
        {
            name = "DeleteTestRole",
            description = "To be deleted",
            permissionNames = Array.Empty<string>()
        });
        var created = await createResponse.Content.ReadFromJsonAsync<CreatedResponseDto>();

        var response = await _client.DeleteAsync($"/api/v1.0/admin/roles/{created!.Id}");
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    // --- Protection Tests ---

    [Fact]
    public async Task DeleteRole_Cannot_Delete_System_Role()
    {
        var adminToken = await CreateUserWithRoleAndGetTokenAsync(
            "admin-roles-delsys@example.com", SystemRoles.SuperAdmin);
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", adminToken);

        // Get system role id
        var listResponse = await _client.GetAsync("/api/v1.0/admin/roles");
        var roles = await listResponse.Content.ReadFromJsonAsync<List<RoleItemDto>>();
        var adminRole = roles!.First(r => r.Name == SystemRoles.Admin);

        var response = await _client.DeleteAsync($"/api/v1.0/admin/roles/{adminRole.Id}");
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdateRole_Cannot_Modify_SuperAdmin()
    {
        var adminToken = await CreateUserWithRoleAndGetTokenAsync(
            "admin-roles-updsa@example.com", SystemRoles.SuperAdmin);
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", adminToken);

        var listResponse = await _client.GetAsync("/api/v1.0/admin/roles");
        var roles = await listResponse.Content.ReadFromJsonAsync<List<RoleItemDto>>();
        var superAdminRole = roles!.First(r => r.Name == SystemRoles.SuperAdmin);

        var response = await _client.PutAsJsonAsync($"/api/v1.0/admin/roles/{superAdminRole.Id}", new
        {
            name = SystemRoles.SuperAdmin,
            description = "Modified",
            permissionNames = new[] { "Users.Read" }
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // --- Permissions Endpoint ---

    [Fact]
    public async Task GetPermissions_Returns_All_Permissions()
    {
        var adminToken = await CreateUserWithRoleAndGetTokenAsync(
            "admin-roles-perms@example.com", SystemRoles.Admin);
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", adminToken);

        var response = await _client.GetAsync("/api/v1.0/admin/roles/permissions");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<List<PermissionItemDto>>();
        body.Should().NotBeNull();
        body!.Count.Should().Be(16);
        body.Should().Contain(p => p.Name == "Users.Read");
        body.Should().Contain(p => p.Name == "Roles.Create");
    }

    [Fact]
    public async Task CreateRole_With_Duplicate_Name_Returns_BadRequest()
    {
        var adminToken = await CreateUserWithRoleAndGetTokenAsync(
            "admin-roles-dup@example.com", SystemRoles.Admin);
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", adminToken);

        await _client.PostAsJsonAsync("/api/v1.0/admin/roles", new
        {
            name = "DuplicateRole",
            description = "First",
            permissionNames = Array.Empty<string>()
        });

        var response = await _client.PostAsJsonAsync("/api/v1.0/admin/roles", new
        {
            name = "DuplicateRole",
            description = "Second",
            permissionNames = Array.Empty<string>()
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // --- DTOs ---

    private record AuthResponseDto(
        string AccessToken, string RefreshToken, DateTime ExpiresAt,
        UserResponseDto User, List<string>? Permissions = null, bool MustChangePassword = false);

    private record UserResponseDto(
        Guid Id, string Email, string FirstName, string LastName, List<string>? Roles = null);

    private record RoleItemDto(
        Guid Id, string Name, string? Description, bool IsSystemRole, int UserCount, DateTime CreatedAt);

    private record RoleDetailDto(
        Guid Id, string Name, string? Description, bool IsSystemRole, int UserCount,
        DateTime CreatedAt, List<string> Permissions);

    private record PermissionItemDto(Guid Id, string Name, string? Description, string Category);

    private record CreatedResponseDto(Guid Id);
}
