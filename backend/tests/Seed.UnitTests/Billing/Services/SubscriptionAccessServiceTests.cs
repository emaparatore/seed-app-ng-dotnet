using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Seed.Domain.Entities;
using Seed.Domain.Enums;
using Seed.Infrastructure.Billing.Services;
using Seed.Infrastructure.Persistence;

namespace Seed.UnitTests.Billing.Services;

public class SubscriptionAccessServiceTests : IDisposable
{
    private readonly ApplicationDbContext _dbContext;
    private readonly SubscriptionAccessService _service;

    private static readonly Guid TestUserId = Guid.NewGuid();

    public SubscriptionAccessServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new ApplicationDbContext(options);
        _service = new SubscriptionAccessService(_dbContext);
    }

    private SubscriptionPlan CreatePlan(string name, params string[] featureKeys)
    {
        var plan = new SubscriptionPlan
        {
            Id = Guid.NewGuid(),
            Name = name,
            MonthlyPrice = 9.99m,
            YearlyPrice = 99.99m,
            Status = PlanStatus.Active,
            SortOrder = 1
        };
        foreach (var key in featureKeys)
        {
            plan.Features.Add(new PlanFeature { Id = Guid.NewGuid(), Key = key, Description = key, SortOrder = 0 });
        }
        return plan;
    }

    private UserSubscription CreateSubscription(Guid planId, SubscriptionStatus status = SubscriptionStatus.Active)
    {
        return new UserSubscription
        {
            Id = Guid.NewGuid(),
            UserId = TestUserId,
            PlanId = planId,
            Status = status,
            CurrentPeriodStart = DateTime.UtcNow,
            CurrentPeriodEnd = DateTime.UtcNow.AddMonths(1)
        };
    }

    [Fact]
    public async Task UserHasActivePlan_WithMatchingPlan_ReturnsTrue()
    {
        var plan = CreatePlan("Pro");
        _dbContext.SubscriptionPlans.Add(plan);
        _dbContext.UserSubscriptions.Add(CreateSubscription(plan.Id, SubscriptionStatus.Active));
        await _dbContext.SaveChangesAsync();

        var result = await _service.UserHasActivePlanAsync(TestUserId, ["Pro"]);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task UserHasActivePlan_WithNonMatchingPlan_ReturnsFalse()
    {
        var plan = CreatePlan("Basic");
        _dbContext.SubscriptionPlans.Add(plan);
        _dbContext.UserSubscriptions.Add(CreateSubscription(plan.Id, SubscriptionStatus.Active));
        await _dbContext.SaveChangesAsync();

        var result = await _service.UserHasActivePlanAsync(TestUserId, ["Pro", "Enterprise"]);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task UserHasActivePlan_WithTrialingSubscription_ReturnsTrue()
    {
        var plan = CreatePlan("Pro");
        _dbContext.SubscriptionPlans.Add(plan);
        _dbContext.UserSubscriptions.Add(CreateSubscription(plan.Id, SubscriptionStatus.Trialing));
        await _dbContext.SaveChangesAsync();

        var result = await _service.UserHasActivePlanAsync(TestUserId, ["Pro"]);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task UserHasActivePlan_WithCanceledSubscription_ReturnsFalse()
    {
        var plan = CreatePlan("Pro");
        _dbContext.SubscriptionPlans.Add(plan);
        _dbContext.UserSubscriptions.Add(CreateSubscription(plan.Id, SubscriptionStatus.Canceled));
        await _dbContext.SaveChangesAsync();

        var result = await _service.UserHasActivePlanAsync(TestUserId, ["Pro"]);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task UserHasActivePlan_WithNoSubscription_ReturnsFalse()
    {
        var result = await _service.UserHasActivePlanAsync(TestUserId, ["Pro"]);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task UserHasFeature_WithMatchingFeature_ReturnsTrue()
    {
        var plan = CreatePlan("Pro", "api-access", "export");
        _dbContext.SubscriptionPlans.Add(plan);
        _dbContext.UserSubscriptions.Add(CreateSubscription(plan.Id, SubscriptionStatus.Active));
        await _dbContext.SaveChangesAsync();

        var result = await _service.UserHasFeatureAsync(TestUserId, "api-access");

        result.Should().BeTrue();
    }

    [Fact]
    public async Task UserHasFeature_WithNonMatchingFeature_ReturnsFalse()
    {
        var plan = CreatePlan("Basic", "export");
        _dbContext.SubscriptionPlans.Add(plan);
        _dbContext.UserSubscriptions.Add(CreateSubscription(plan.Id, SubscriptionStatus.Active));
        await _dbContext.SaveChangesAsync();

        var result = await _service.UserHasFeatureAsync(TestUserId, "api-access");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task UserHasFeature_WithExpiredSubscription_ReturnsFalse()
    {
        var plan = CreatePlan("Pro", "api-access");
        _dbContext.SubscriptionPlans.Add(plan);
        _dbContext.UserSubscriptions.Add(CreateSubscription(plan.Id, SubscriptionStatus.Expired));
        await _dbContext.SaveChangesAsync();

        var result = await _service.UserHasFeatureAsync(TestUserId, "api-access");

        result.Should().BeFalse();
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
