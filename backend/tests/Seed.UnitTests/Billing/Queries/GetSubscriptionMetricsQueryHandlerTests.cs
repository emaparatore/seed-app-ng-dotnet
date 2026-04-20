using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Seed.Application.Admin.Subscriptions.Queries.GetSubscriptionMetrics;
using Seed.Domain.Entities;
using Seed.Domain.Enums;
using Seed.Infrastructure.Billing.Queries;
using Seed.Infrastructure.Persistence;

namespace Seed.UnitTests.Billing.Queries;

public class GetSubscriptionMetricsQueryHandlerTests : IDisposable
{
    private readonly ApplicationDbContext _dbContext;
    private readonly GetSubscriptionMetricsQueryHandler _handler;

    public GetSubscriptionMetricsQueryHandlerTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new ApplicationDbContext(options);
        _handler = new GetSubscriptionMetricsQueryHandler(_dbContext);
    }

    [Fact]
    public async Task Should_Return_Zero_Metrics_When_No_Subscriptions()
    {
        var result = await _handler.Handle(new GetSubscriptionMetricsQuery(), CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.Data!.Mrr.Should().Be(0);
        result.Data.ActiveCount.Should().Be(0);
        result.Data.TrialingCount.Should().Be(0);
        result.Data.ChurnRate.Should().Be(0);
    }

    [Fact]
    public async Task Should_Calculate_Mrr_From_Active_Monthly_Subscriptions()
    {
        var plan = CreatePlan(monthlyPrice: 10m, yearlyPrice: 100m);
        _dbContext.SubscriptionPlans.Add(plan);
        _dbContext.UserSubscriptions.AddRange(
            CreateSubscription(plan.Id, SubscriptionStatus.Active, monthly: true),
            CreateSubscription(plan.Id, SubscriptionStatus.Active, monthly: true));
        await _dbContext.SaveChangesAsync();

        var result = await _handler.Handle(new GetSubscriptionMetricsQuery(), CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.Data!.Mrr.Should().Be(20m);
    }

    [Fact]
    public async Task Should_Include_Trialing_Subscriptions_In_Mrr()
    {
        var plan = CreatePlan(monthlyPrice: 10m, yearlyPrice: 120m);
        _dbContext.SubscriptionPlans.Add(plan);
        _dbContext.UserSubscriptions.AddRange(
            CreateSubscription(plan.Id, SubscriptionStatus.Active, monthly: true),
            CreateSubscription(plan.Id, SubscriptionStatus.Trialing, monthly: true));
        await _dbContext.SaveChangesAsync();

        var result = await _handler.Handle(new GetSubscriptionMetricsQuery(), CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.Data!.Mrr.Should().Be(20m);
        result.Data.ActiveCount.Should().Be(1);
        result.Data.TrialingCount.Should().Be(1);
    }

    [Fact]
    public async Task Should_Use_YearlyPrice_Divided_By_12_For_Yearly_Billing()
    {
        var plan = CreatePlan(monthlyPrice: 10m, yearlyPrice: 120m);
        _dbContext.SubscriptionPlans.Add(plan);
        _dbContext.UserSubscriptions.Add(
            CreateSubscription(plan.Id, SubscriptionStatus.Active, monthly: false));
        await _dbContext.SaveChangesAsync();

        var result = await _handler.Handle(new GetSubscriptionMetricsQuery(), CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.Data!.Mrr.Should().Be(10m); // 120 / 12
    }

    [Fact]
    public async Task Should_Calculate_Churn_Rate_Correctly()
    {
        var plan = CreatePlan(monthlyPrice: 10m, yearlyPrice: 100m);
        _dbContext.SubscriptionPlans.Add(plan);
        _dbContext.UserSubscriptions.AddRange(
            CreateSubscription(plan.Id, SubscriptionStatus.Active, monthly: true),
            CreateSubscription(plan.Id, SubscriptionStatus.Active, monthly: true),
            CreateSubscription(plan.Id, SubscriptionStatus.Active, monthly: true),
            CreateCanceledSubscription(plan.Id, DateTime.UtcNow.AddDays(-10)));
        await _dbContext.SaveChangesAsync();

        var result = await _handler.Handle(new GetSubscriptionMetricsQuery(), CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        // churnRate = 1 / (3 + 0 + 1) = 0.25
        result.Data!.ChurnRate.Should().Be(0.25m);
    }

    [Fact]
    public async Task Should_Not_Include_Old_Canceled_In_Churn()
    {
        var plan = CreatePlan(monthlyPrice: 10m, yearlyPrice: 100m);
        _dbContext.SubscriptionPlans.Add(plan);
        _dbContext.UserSubscriptions.AddRange(
            CreateSubscription(plan.Id, SubscriptionStatus.Active, monthly: true),
            CreateCanceledSubscription(plan.Id, DateTime.UtcNow.AddDays(-60))); // older than 30 days
        await _dbContext.SaveChangesAsync();

        var result = await _handler.Handle(new GetSubscriptionMetricsQuery(), CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        // canceledLast30Days = 0, denominator = 1, churnRate = 0
        result.Data!.ChurnRate.Should().Be(0m);
    }

    private static SubscriptionPlan CreatePlan(decimal monthlyPrice, decimal yearlyPrice) => new()
    {
        Id = Guid.NewGuid(),
        Name = "Test Plan",
        MonthlyPrice = monthlyPrice,
        YearlyPrice = yearlyPrice,
        Status = PlanStatus.Active
    };

    private static UserSubscription CreateSubscription(Guid planId, SubscriptionStatus status, bool monthly) => new()
    {
        Id = Guid.NewGuid(),
        UserId = Guid.NewGuid(),
        PlanId = planId,
        Status = status,
        CurrentPeriodStart = DateTime.UtcNow,
        CurrentPeriodEnd = monthly ? DateTime.UtcNow.AddDays(30) : DateTime.UtcNow.AddDays(365)
    };

    private static UserSubscription CreateCanceledSubscription(Guid planId, DateTime canceledAt) => new()
    {
        Id = Guid.NewGuid(),
        UserId = Guid.NewGuid(),
        PlanId = planId,
        Status = SubscriptionStatus.Canceled,
        CanceledAt = canceledAt,
        CurrentPeriodStart = DateTime.UtcNow.AddMonths(-1),
        CurrentPeriodEnd = DateTime.UtcNow
    };

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
