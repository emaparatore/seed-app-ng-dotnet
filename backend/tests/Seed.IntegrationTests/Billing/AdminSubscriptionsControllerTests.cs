using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Seed.Domain.Authorization;
using Seed.Domain.Entities;
using Seed.Domain.Enums;
using Seed.Infrastructure.Persistence;
using Seed.IntegrationTests.Webhooks;

namespace Seed.IntegrationTests.Billing;

public class AdminSubscriptionsControllerTests : IClassFixture<WebhookWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly WebhookWebApplicationFactory _factory;

    public AdminSubscriptionsControllerTests(WebhookWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task<string> CreateAdminUserAndGetTokenAsync(string email)
    {
        await _client.PostAsJsonAsync("/api/v1.0/auth/register", new
        {
            email,
            password = "Password1",
            firstName = "Admin",
            lastName = "User",
            acceptPrivacyPolicy = true,
            acceptTermsOfService = true
        });

        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync(email);
        var confirmToken = await userManager.GenerateEmailConfirmationTokenAsync(user!);
        await _client.PostAsJsonAsync("/api/v1.0/auth/confirm-email", new { email, token = confirmToken });

        using var scope2 = _factory.Services.CreateScope();
        var userManager2 = scope2.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user2 = await userManager2.FindByEmailAsync(email);
        await userManager2.AddToRoleAsync(user2!, SystemRoles.Admin);

        var loginResponse = await _client.PostAsJsonAsync("/api/v1.0/auth/login", new { email, password = "Password1" });
        loginResponse.EnsureSuccessStatusCode();
        var auth = await loginResponse.Content.ReadFromJsonAsync<AuthResponseDto>();
        return auth!.AccessToken;
    }

    [Fact]
    public async Task GetMetrics_WithoutAuth_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/api/v1.0/admin/subscriptions/metrics");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetMetrics_ReturnsOk_WithMetrics()
    {
        var email = $"admin-metrics-{Guid.NewGuid():N}@example.com";
        var token = await CreateAdminUserAndGetTokenAsync(email);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync(email);

        var plan = new SubscriptionPlan
        {
            Id = Guid.NewGuid(),
            Name = $"Metrics Plan {Guid.NewGuid():N}",
            MonthlyPrice = 20m,
            YearlyPrice = 200m,
            Status = PlanStatus.Active,
            SortOrder = 99
        };
        db.SubscriptionPlans.Add(plan);
        db.UserSubscriptions.Add(new UserSubscription
        {
            Id = Guid.NewGuid(),
            UserId = user!.Id,
            PlanId = plan.Id,
            Status = SubscriptionStatus.Active,
            CurrentPeriodStart = DateTime.UtcNow,
            CurrentPeriodEnd = DateTime.UtcNow.AddDays(30)
        });
        await db.SaveChangesAsync();

        var response = await _client.GetAsync("/api/v1.0/admin/subscriptions/metrics");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var metrics = await response.Content.ReadFromJsonAsync<MetricsResponseDto>();
        metrics.Should().NotBeNull();
        metrics!.Mrr.Should().BeGreaterThan(0);
        metrics.ActiveCount.Should().BeGreaterThanOrEqualTo(1);

        // Cleanup
        db.UserSubscriptions.RemoveRange(db.UserSubscriptions.Where(s => s.PlanId == plan.Id));
        db.SubscriptionPlans.Remove(plan);
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task GetSubscriptions_ReturnsPagedList_WithFilters()
    {
        var email = $"admin-list-{Guid.NewGuid():N}@example.com";
        var token = await CreateAdminUserAndGetTokenAsync(email);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync(email);

        var plan = new SubscriptionPlan
        {
            Id = Guid.NewGuid(),
            Name = $"List Plan {Guid.NewGuid():N}",
            MonthlyPrice = 15m,
            YearlyPrice = 150m,
            Status = PlanStatus.Active,
            SortOrder = 98
        };
        db.SubscriptionPlans.Add(plan);
        db.UserSubscriptions.Add(new UserSubscription
        {
            Id = Guid.NewGuid(),
            UserId = user!.Id,
            PlanId = plan.Id,
            Status = SubscriptionStatus.Active,
            CurrentPeriodStart = DateTime.UtcNow,
            CurrentPeriodEnd = DateTime.UtcNow.AddDays(30)
        });
        await db.SaveChangesAsync();

        var response = await _client.GetAsync(
            $"/api/v1.0/admin/subscriptions?pageNumber=1&pageSize=10&planId={plan.Id}&status=Active");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<PagedResponseDto>();
        result.Should().NotBeNull();
        result!.Items.Should().NotBeEmpty();
        result.Items.Should().AllSatisfy(s => s.Status.Should().Be("Active"));

        // Cleanup
        db.UserSubscriptions.RemoveRange(db.UserSubscriptions.Where(s => s.PlanId == plan.Id));
        db.SubscriptionPlans.Remove(plan);
        await db.SaveChangesAsync();
    }

    private record AuthResponseDto(string AccessToken, string RefreshToken, DateTime ExpiresAt, UserResponseDto User);
    private record UserResponseDto(Guid Id, string Email, string FirstName, string LastName);
    private record MetricsResponseDto(decimal Mrr, int ActiveCount, int TrialingCount, decimal ChurnRate);
    private record PagedResponseDto(List<SubscriptionItemDto> Items, int PageNumber, int PageSize, int TotalCount);
    private record SubscriptionItemDto(Guid Id, string UserEmail, string PlanName, string Status);
}
