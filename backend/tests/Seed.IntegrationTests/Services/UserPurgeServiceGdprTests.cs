using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Seed.Application.Common.Interfaces;
using Seed.Domain.Entities;
using Seed.Domain.Enums;
using Seed.Infrastructure.Persistence;
using Seed.IntegrationTests.Infrastructure;
using Seed.IntegrationTests.Webhooks;

namespace Seed.IntegrationTests.Services;

/// <summary>
/// Integration tests for GDPR subscription/invoice anonymization in UserPurgeService.
/// Uses WebhookWebApplicationFactory (payments module enabled + MockPaymentGateway).
/// </summary>
public class UserPurgeServiceGdprTests(WebhookWebApplicationFactory factory)
    : IClassFixture<WebhookWebApplicationFactory>
{
    private async Task<(ApplicationUser User, IServiceScope Scope)> CreateTestUserAsync()
    {
        var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var user = new ApplicationUser
        {
            Email = $"gdpr-purge-{Guid.NewGuid():N}@test.com",
            UserName = $"gdpr-purge-{Guid.NewGuid():N}@test.com",
            FirstName = "GDPR",
            LastName = "Test",
            IsActive = true,
            EmailConfirmed = true
        };

        var result = await userManager.CreateAsync(user, "Password1!");
        result.Succeeded.Should().BeTrue();

        return (user, scope);
    }

    private static async Task<SubscriptionPlan> GetOrCreatePlanAsync(ApplicationDbContext dbContext)
    {
        var plan = await dbContext.SubscriptionPlans.FirstOrDefaultAsync();
        if (plan is not null)
            return plan;

        plan = new SubscriptionPlan
        {
            Id = Guid.NewGuid(),
            Name = "Test Plan",
            MonthlyPrice = 9.99m,
            YearlyPrice = 99m,
            Status = PlanStatus.Active,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        dbContext.SubscriptionPlans.Add(plan);
        await dbContext.SaveChangesAsync();
        return plan;
    }

    [Fact]
    public async Task PurgeUserAsync_AnonymizesSubscriptions_WhenPaymentsModuleEnabled()
    {
        var (user, scope) = await CreateTestUserAsync();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var purgeService = scope.ServiceProvider.GetRequiredService<IUserPurgeService>();

        var plan = await GetOrCreatePlanAsync(dbContext);

        var subscription = new UserSubscription
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            PlanId = plan.Id,
            Status = SubscriptionStatus.Active,
            StripeSubscriptionId = $"sub_test_{Guid.NewGuid():N}",
            StripeCustomerId = $"cus_test_{Guid.NewGuid():N}",
            CurrentPeriodStart = DateTime.UtcNow.AddDays(-15),
            CurrentPeriodEnd = DateTime.UtcNow.AddDays(15),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        dbContext.UserSubscriptions.Add(subscription);
        await dbContext.SaveChangesAsync();

        await purgeService.PurgeUserAsync(user.Id);

        var anonymized = await dbContext.UserSubscriptions
            .FirstOrDefaultAsync(s => s.Id == subscription.Id);

        anonymized.Should().NotBeNull();
        anonymized!.UserId.Should().BeNull();
        anonymized.Status.Should().Be(SubscriptionStatus.Canceled);

        scope.Dispose();
    }

    [Fact]
    public async Task PurgeUserAsync_AnonymizesInvoiceRequests_KeepsFiscalData()
    {
        var (user, scope) = await CreateTestUserAsync();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var purgeService = scope.ServiceProvider.GetRequiredService<IUserPurgeService>();

        var plan = await GetOrCreatePlanAsync(dbContext);

        var invoiceRequest = new InvoiceRequest
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            CustomerType = CustomerType.Individual,
            FullName = "Mario Rossi",
            Address = "Via Roma 1",
            City = "Milano",
            PostalCode = "20100",
            Country = "IT",
            FiscalCode = "RSSMRA80A01H501U",
            VatNumber = null,
            PecEmail = "mario.rossi@pec.it",
            Status = InvoiceRequestStatus.Requested,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        dbContext.InvoiceRequests.Add(invoiceRequest);
        await dbContext.SaveChangesAsync();

        await purgeService.PurgeUserAsync(user.Id);

        var anonymized = await dbContext.InvoiceRequests
            .FirstOrDefaultAsync(i => i.Id == invoiceRequest.Id);

        anonymized.Should().NotBeNull();
        anonymized!.UserId.Should().BeNull();
        anonymized.FullName.Should().Be("ANONYMIZED");
        anonymized.Address.Should().Be("ANONYMIZED");
        anonymized.City.Should().Be("ANONYMIZED");
        anonymized.PostalCode.Should().Be("ANONYMIZED");
        anonymized.PecEmail.Should().BeNull();
        // Fiscal data retained for compliance
        anonymized.FiscalCode.Should().Be("RSSMRA80A01H501U");
        anonymized.Country.Should().Be("IT");
        anonymized.Status.Should().Be(InvoiceRequestStatus.Requested);

        scope.Dispose();
    }

    [Fact]
    public async Task PurgeUserAsync_SkipsPaymentCleanup_WhenModuleDisabled()
    {
        // Use CustomWebApplicationFactory (payments module NOT enabled)
        await using var plainFactory = new CustomWebApplicationFactory();
        await plainFactory.InitializeAsync();

        using var scope = plainFactory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var purgeService = scope.ServiceProvider.GetRequiredService<IUserPurgeService>();

        var user = new ApplicationUser
        {
            Email = $"no-payments-{Guid.NewGuid():N}@test.com",
            UserName = $"no-payments-{Guid.NewGuid():N}@test.com",
            FirstName = "No",
            LastName = "Payments",
            IsActive = true,
            EmailConfirmed = true
        };
        (await userManager.CreateAsync(user, "Password1!")).Succeeded.Should().BeTrue();

        // Purge should complete without error even when IPaymentGateway is not registered
        var act = () => purgeService.PurgeUserAsync(user.Id);
        await act.Should().NotThrowAsync();

        var deletedUser = await dbContext.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == user.Id);
        deletedUser.Should().BeNull();

        scope.Dispose();
    }
}
