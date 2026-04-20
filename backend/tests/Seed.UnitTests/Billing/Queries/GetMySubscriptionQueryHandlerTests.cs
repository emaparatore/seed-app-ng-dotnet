using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Seed.Application.Billing.Queries.GetMySubscription;
using Seed.Domain.Entities;
using Seed.Domain.Enums;
using Seed.Infrastructure.Billing.Queries;
using Seed.Infrastructure.Persistence;

namespace Seed.UnitTests.Billing.Queries;

public class GetMySubscriptionQueryHandlerTests : IDisposable
{
    private readonly ApplicationDbContext _dbContext;
    private readonly GetMySubscriptionQueryHandler _handler;

    private static readonly Guid TestUserId = Guid.NewGuid();

    public GetMySubscriptionQueryHandlerTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new ApplicationDbContext(options);
        _handler = new GetMySubscriptionQueryHandler(_dbContext);
    }

    private SubscriptionPlan CreatePlan(string name = "Pro Plan")
    {
        var plan = new SubscriptionPlan
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = "A pro plan",
            MonthlyPrice = 9.99m,
            YearlyPrice = 99.99m,
            Status = PlanStatus.Active,
            IsFreeTier = false,
            SortOrder = 1
        };
        return plan;
    }

    private UserSubscription CreateSubscription(
        Guid planId,
        SubscriptionStatus status = SubscriptionStatus.Active)
    {
        return new UserSubscription
        {
            Id = Guid.NewGuid(),
            UserId = TestUserId,
            PlanId = planId,
            Status = status,
            StripeSubscriptionId = "sub_test_123",
            StripeCustomerId = "cus_test_123",
            CurrentPeriodStart = DateTime.UtcNow,
            CurrentPeriodEnd = DateTime.UtcNow.AddMonths(1)
        };
    }

    [Fact]
    public async Task Should_Return_Subscription_When_Active_Subscription_Exists()
    {
        var plan = CreatePlan();
        _dbContext.SubscriptionPlans.Add(plan);
        _dbContext.UserSubscriptions.Add(CreateSubscription(plan.Id));
        await _dbContext.SaveChangesAsync();

        var result = await _handler.Handle(new GetMySubscriptionQuery(TestUserId), CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.PlanName.Should().Be("Pro Plan");
        result.Data.Status.Should().Be("Active");
        result.Data.MonthlyPrice.Should().Be(9.99m);
        result.Data.YearlyPrice.Should().Be(99.99m);
    }

    [Fact]
    public async Task Should_Return_Subscription_When_Status_Is_Trialing()
    {
        var plan = CreatePlan();
        _dbContext.SubscriptionPlans.Add(plan);
        _dbContext.UserSubscriptions.Add(CreateSubscription(plan.Id, SubscriptionStatus.Trialing));
        await _dbContext.SaveChangesAsync();

        var result = await _handler.Handle(new GetMySubscriptionQuery(TestUserId), CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Status.Should().Be("Trialing");
    }

    [Fact]
    public async Task Should_Return_Null_When_No_Subscription_Exists()
    {
        var result = await _handler.Handle(new GetMySubscriptionQuery(TestUserId), CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.Data.Should().BeNull();
    }

    [Theory]
    [InlineData(SubscriptionStatus.Canceled)]
    [InlineData(SubscriptionStatus.Expired)]
    public async Task Should_Return_Null_When_Subscription_Is_Not_Active_Or_Trialing(SubscriptionStatus status)
    {
        var plan = CreatePlan();
        _dbContext.SubscriptionPlans.Add(plan);
        _dbContext.UserSubscriptions.Add(CreateSubscription(plan.Id, status));
        await _dbContext.SaveChangesAsync();

        var result = await _handler.Handle(new GetMySubscriptionQuery(TestUserId), CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.Data.Should().BeNull();
    }

    [Fact]
    public async Task Should_Include_Plan_Features_In_Result()
    {
        var plan = CreatePlan();
        plan.Features.Add(new PlanFeature
        {
            Id = Guid.NewGuid(),
            Key = "storage",
            Description = "10 GB Storage",
            LimitValue = "10",
            SortOrder = 1
        });
        plan.Features.Add(new PlanFeature
        {
            Id = Guid.NewGuid(),
            Key = "users",
            Description = "5 Users",
            LimitValue = "5",
            SortOrder = 2
        });
        _dbContext.SubscriptionPlans.Add(plan);
        _dbContext.UserSubscriptions.Add(CreateSubscription(plan.Id));
        await _dbContext.SaveChangesAsync();

        var result = await _handler.Handle(new GetMySubscriptionQuery(TestUserId), CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Features.Should().HaveCount(2);
        result.Data.Features[0].Key.Should().Be("storage");
        result.Data.Features[1].Key.Should().Be("users");
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
