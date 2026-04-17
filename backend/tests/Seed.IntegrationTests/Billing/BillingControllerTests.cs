using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Seed.Application.Billing.Models;
using Seed.Domain.Entities;
using Seed.Domain.Enums;
using Seed.Infrastructure.Persistence;
using Seed.IntegrationTests.Webhooks;

namespace Seed.IntegrationTests.Billing;

public class BillingControllerTests : IClassFixture<WebhookWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly WebhookWebApplicationFactory _factory;

    public BillingControllerTests(WebhookWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task<AuthResponseDto> RegisterAndConfirmUserAsync(string email)
    {
        var regResponse = await _client.PostAsJsonAsync("/api/v1.0/auth/register", new
        {
            email,
            password = "Password1",
            firstName = "Test",
            lastName = "User",
            acceptPrivacyPolicy = true,
            acceptTermsOfService = true
        });
        regResponse.EnsureSuccessStatusCode();

        using var scope = _factory.Services.CreateScope();
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

    private async Task<Guid> GetUserIdAsync(string email)
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync(email);
        return Guid.Parse(user!.Id.ToString());
    }

    [Fact]
    public async Task GetSubscription_Returns_Ok_With_Data_When_Active_Subscription_Exists()
    {
        var email = $"billing-get-sub-{Guid.NewGuid():N}@example.com";
        var auth = await RegisterAndConfirmUserAsync(email);
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        var userId = await GetUserIdAsync(email);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var plan = new SubscriptionPlan
        {
            Id = Guid.NewGuid(),
            Name = "Billing Test Plan",
            Description = "Test plan for billing",
            MonthlyPrice = 19.99m,
            YearlyPrice = 199.99m,
            Status = PlanStatus.Active,
            SortOrder = 1
        };
        plan.Features.Add(new PlanFeature
        {
            Id = Guid.NewGuid(),
            Key = "api_calls",
            Description = "1000 API calls",
            SortOrder = 1
        });
        db.SubscriptionPlans.Add(plan);

        db.UserSubscriptions.Add(new UserSubscription
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            PlanId = plan.Id,
            Status = SubscriptionStatus.Active,
            StripeSubscriptionId = "sub_integration_test",
            StripeCustomerId = "cus_integration_test",
            CurrentPeriodStart = DateTime.UtcNow,
            CurrentPeriodEnd = DateTime.UtcNow.AddMonths(1)
        });
        await db.SaveChangesAsync();

        var response = await _client.GetAsync("/api/v1.0/billing/subscription");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var subscription = await response.Content.ReadFromJsonAsync<UserSubscriptionDto>();
        subscription.Should().NotBeNull();
        subscription!.PlanName.Should().Be("Billing Test Plan");
        subscription.Features.Should().HaveCount(1);

        // Cleanup
        db.UserSubscriptions.RemoveRange(db.UserSubscriptions.Where(s => s.UserId == userId));
        db.SubscriptionPlans.Remove(plan);
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task GetSubscription_Returns_Ok_With_Null_When_No_Subscription()
    {
        var email = $"billing-no-sub-{Guid.NewGuid():N}@example.com";
        var auth = await RegisterAndConfirmUserAsync(email);
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        var response = await _client.GetAsync("/api/v1.0/billing/subscription");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().BeOneOf("null", "");
    }

    [Fact]
    public async Task Cancel_Returns_Ok_When_Active_Subscription_Exists()
    {
        var email = $"billing-cancel-{Guid.NewGuid():N}@example.com";
        var auth = await RegisterAndConfirmUserAsync(email);
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        var userId = await GetUserIdAsync(email);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var plan = new SubscriptionPlan
        {
            Id = Guid.NewGuid(),
            Name = "Cancel Test Plan",
            MonthlyPrice = 9.99m,
            YearlyPrice = 99.99m,
            Status = PlanStatus.Active,
            SortOrder = 1
        };
        db.SubscriptionPlans.Add(plan);

        db.UserSubscriptions.Add(new UserSubscription
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            PlanId = plan.Id,
            Status = SubscriptionStatus.Active,
            StripeSubscriptionId = "sub_cancel_test",
            StripeCustomerId = "cus_cancel_test",
            CurrentPeriodStart = DateTime.UtcNow,
            CurrentPeriodEnd = DateTime.UtcNow.AddMonths(1)
        });
        await db.SaveChangesAsync();

        var response = await _client.PostAsync("/api/v1.0/billing/cancel", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Cleanup
        db.UserSubscriptions.RemoveRange(db.UserSubscriptions.Where(s => s.UserId == userId));
        db.SubscriptionPlans.Remove(plan);
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Portal_Returns_Ok_With_PortalUrl()
    {
        var email = $"billing-portal-{Guid.NewGuid():N}@example.com";
        var auth = await RegisterAndConfirmUserAsync(email);
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        var userId = await GetUserIdAsync(email);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var plan = new SubscriptionPlan
        {
            Id = Guid.NewGuid(),
            Name = "Portal Test Plan",
            MonthlyPrice = 9.99m,
            YearlyPrice = 99.99m,
            Status = PlanStatus.Active,
            SortOrder = 1
        };
        db.SubscriptionPlans.Add(plan);

        db.UserSubscriptions.Add(new UserSubscription
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            PlanId = plan.Id,
            Status = SubscriptionStatus.Active,
            StripeSubscriptionId = "sub_portal_test",
            StripeCustomerId = "cus_portal_test",
            CurrentPeriodStart = DateTime.UtcNow,
            CurrentPeriodEnd = DateTime.UtcNow.AddMonths(1)
        });
        await db.SaveChangesAsync();

        var response = await _client.PostAsJsonAsync("/api/v1.0/billing/portal", new
        {
            returnUrl = "https://example.com/account"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<PortalSessionResponse>();
        result.Should().NotBeNull();
        result!.PortalUrl.Should().NotBeNullOrEmpty();

        // Cleanup
        db.UserSubscriptions.RemoveRange(db.UserSubscriptions.Where(s => s.UserId == userId));
        db.SubscriptionPlans.Remove(plan);
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task ChangePlan_Returns_Ok_And_Updates_Subscription_Plan()
    {
        var email = $"billing-change-plan-{Guid.NewGuid():N}@example.com";
        var auth = await RegisterAndConfirmUserAsync(email);
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        var userId = await GetUserIdAsync(email);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var oldPlan = new SubscriptionPlan
        {
            Id = Guid.NewGuid(),
            Name = "Old Plan",
            MonthlyPrice = 9.99m,
            YearlyPrice = 99.99m,
            StripePriceIdMonthly = "price_old_monthly",
            StripePriceIdYearly = "price_old_yearly",
            Status = PlanStatus.Active,
            SortOrder = 1
        };

        var newPlan = new SubscriptionPlan
        {
            Id = Guid.NewGuid(),
            Name = "New Plan",
            MonthlyPrice = 19.99m,
            YearlyPrice = 199.99m,
            StripePriceIdMonthly = "price_new_monthly",
            StripePriceIdYearly = "price_new_yearly",
            Status = PlanStatus.Active,
            SortOrder = 2
        };

        db.SubscriptionPlans.AddRange(oldPlan, newPlan);

        var subscriptionId = Guid.NewGuid();
        db.UserSubscriptions.Add(new UserSubscription
        {
            Id = subscriptionId,
            UserId = userId,
            PlanId = oldPlan.Id,
            Status = SubscriptionStatus.Active,
            StripeSubscriptionId = "sub_change_plan_test",
            StripeCustomerId = "cus_change_plan_test",
            CurrentPeriodStart = DateTime.UtcNow,
            CurrentPeriodEnd = DateTime.UtcNow.AddMonths(1)
        });
        await db.SaveChangesAsync();

        var response = await _client.PostAsJsonAsync("/api/v1/billing/change-plan", new
        {
            planId = newPlan.Id,
            billingInterval = "Monthly"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await using var verificationScope = _factory.Services.CreateAsyncScope();
        var verificationDb = verificationScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var updatedSubscription = await verificationDb.UserSubscriptions.FindAsync(subscriptionId);
        updatedSubscription.Should().NotBeNull();
        updatedSubscription!.PlanId.Should().Be(newPlan.Id);
        updatedSubscription.ScheduledPlanId.Should().BeNull();
        updatedSubscription.StripeScheduleId.Should().BeNull();

        // Cleanup
        var subscriptionsToDelete = db.UserSubscriptions
            .Where(s => s.UserId == userId || s.PlanId == oldPlan.Id || s.PlanId == newPlan.Id)
            .ToList();
        db.UserSubscriptions.RemoveRange(subscriptionsToDelete);
        await db.SaveChangesAsync();

        db.SubscriptionPlans.RemoveRange(oldPlan, newPlan);
        await db.SaveChangesAsync();
    }

    private record AuthResponseDto(
        string AccessToken, string RefreshToken, DateTime ExpiresAt, UserResponseDto User);

    private record UserResponseDto(
        Guid Id, string Email, string FirstName, string LastName);
}
