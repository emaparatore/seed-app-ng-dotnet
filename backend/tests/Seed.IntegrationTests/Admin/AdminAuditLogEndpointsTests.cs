using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Seed.Application.Common.Interfaces;
using Seed.Domain.Authorization;
using Seed.Domain.Entities;
using Seed.IntegrationTests.Infrastructure;

namespace Seed.IntegrationTests.Admin;

public class AdminAuditLogEndpointsTests(CustomWebApplicationFactory factory)
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

    private async Task SeedAuditLogEntriesAsync()
    {
        using var scope = factory.Services.CreateScope();
        var auditService = scope.ServiceProvider.GetRequiredService<IAuditService>();

        await auditService.LogAsync("UserCreated", "User", "1",
            "{\"email\":\"created@example.com\"}", Guid.NewGuid(), "127.0.0.1", "TestAgent");
        await auditService.LogAsync("LoginSuccess", "Auth", null,
            "{\"method\":\"password\"}", Guid.NewGuid(), "192.168.1.1", "TestAgent");
        await auditService.LogAsync("RoleCreated", "Role", "2",
            "{\"name\":\"Editor\"}", Guid.NewGuid(), "10.0.0.1", "TestAgent");
    }

    // --- Authorization Tests ---

    [Fact]
    public async Task GetAuditLog_Without_Permission_Returns_Forbidden()
    {
        await _client.PostAsJsonAsync("/api/v1.0/auth/register", new
        {
            email = "noperm-audit@example.com",
            password = "Password1",
            firstName = "No",
            lastName = "Perm",
            acceptPrivacyPolicy = true,
            acceptTermsOfService = true
        });

        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync("noperm-audit@example.com");
        var token = await userManager.GenerateEmailConfirmationTokenAsync(user!);
        await _client.PostAsJsonAsync("/api/v1.0/auth/confirm-email",
            new { email = "noperm-audit@example.com", token });

        var loginResponse = await _client.PostAsJsonAsync("/api/v1.0/auth/login", new
        {
            email = "noperm-audit@example.com",
            password = "Password1"
        });
        var auth = await loginResponse.Content.ReadFromJsonAsync<AuthResponseDto>();

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", auth!.AccessToken);

        var response = await _client.GetAsync("/api/v1.0/admin/audit-log");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetAuditLog_With_Admin_Role_Returns_Ok()
    {
        await SeedAuditLogEntriesAsync();

        var adminToken = await CreateUserWithRoleAndGetTokenAsync(
            "admin-auditlog-list@example.com", SystemRoles.Admin);
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", adminToken);

        var response = await _client.GetAsync("/api/v1.0/admin/audit-log");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<PagedResultDto>();
        body.Should().NotBeNull();
        body!.Items.Should().NotBeEmpty();
        body.TotalCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetAuditLogById_Returns_Entry_Detail()
    {
        await SeedAuditLogEntriesAsync();

        var adminToken = await CreateUserWithRoleAndGetTokenAsync(
            "admin-auditlog-detail@example.com", SystemRoles.Admin);
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", adminToken);

        // First get the list to obtain an ID
        var listResponse = await _client.GetAsync("/api/v1.0/admin/audit-log?pageSize=1");
        var list = await listResponse.Content.ReadFromJsonAsync<PagedResultDto>();
        var entryId = list!.Items[0].Id;

        var response = await _client.GetAsync($"/api/v1.0/admin/audit-log/{entryId}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var entry = await response.Content.ReadFromJsonAsync<AuditLogEntryDto>();
        entry.Should().NotBeNull();
        entry!.Id.Should().Be(entryId);
    }

    [Fact]
    public async Task GetAuditLog_With_ActionFilter_Returns_Filtered_Results()
    {
        await SeedAuditLogEntriesAsync();

        var adminToken = await CreateUserWithRoleAndGetTokenAsync(
            "admin-auditlog-filter@example.com", SystemRoles.Admin);
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", adminToken);

        var response = await _client.GetAsync("/api/v1.0/admin/audit-log?actionFilter=UserCreated");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<PagedResultDto>();
        body.Should().NotBeNull();
        body!.Items.Should().AllSatisfy(i => i.Action.Should().Be("UserCreated"));
    }

    [Fact]
    public async Task ExportAuditLog_Without_Permission_Returns_Forbidden()
    {
        await _client.PostAsJsonAsync("/api/v1.0/auth/register", new
        {
            email = "noperm-export@example.com",
            password = "Password1",
            firstName = "No",
            lastName = "Export",
            acceptPrivacyPolicy = true,
            acceptTermsOfService = true
        });

        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync("noperm-export@example.com");
        var token = await userManager.GenerateEmailConfirmationTokenAsync(user!);
        await _client.PostAsJsonAsync("/api/v1.0/auth/confirm-email",
            new { email = "noperm-export@example.com", token });

        var loginResponse = await _client.PostAsJsonAsync("/api/v1.0/auth/login", new
        {
            email = "noperm-export@example.com",
            password = "Password1"
        });
        var auth = await loginResponse.Content.ReadFromJsonAsync<AuthResponseDto>();

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", auth!.AccessToken);

        var response = await _client.GetAsync("/api/v1.0/admin/audit-log/export");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ExportAuditLog_With_Admin_Returns_CSV()
    {
        await SeedAuditLogEntriesAsync();

        var adminToken = await CreateUserWithRoleAndGetTokenAsync(
            "admin-auditlog-export@example.com", SystemRoles.Admin);
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", adminToken);

        var response = await _client.GetAsync("/api/v1.0/admin/audit-log/export");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("text/csv");

        var bytes = await response.Content.ReadAsByteArrayAsync();
        var csv = Encoding.UTF8.GetString(bytes).TrimStart('\uFEFF');
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        lines[0].Trim().Should().Be("Id,Timestamp,UserId,Action,EntityType,EntityId,Details,IpAddress,UserAgent");
        lines.Length.Should().BeGreaterThan(1);
    }

    // --- DTOs ---

    private record AuthResponseDto(
        string AccessToken, string RefreshToken, DateTime ExpiresAt,
        UserResponseDto User, List<string>? Permissions = null, bool MustChangePassword = false);

    private record UserResponseDto(
        Guid Id, string Email, string FirstName, string LastName, List<string>? Roles = null);

    private record AuditLogEntryDto(
        Guid Id, DateTime Timestamp, Guid? UserId, string Action, string EntityType,
        string? EntityId, string? Details, string? IpAddress, string? UserAgent);

    private record PagedResultDto(
        List<AuditLogEntryDto> Items, int PageNumber, int PageSize, int TotalCount,
        int TotalPages, bool HasPreviousPage, bool HasNextPage);
}
