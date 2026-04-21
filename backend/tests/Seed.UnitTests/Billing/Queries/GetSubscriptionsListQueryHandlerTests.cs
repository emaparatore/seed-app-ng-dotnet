using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Seed.Application.Admin.Subscriptions.Queries.GetSubscriptionsList;
using Seed.Domain.Entities;
using Seed.Domain.Enums;
using Seed.Infrastructure.Billing.Queries;
using Seed.Infrastructure.Persistence;

namespace Seed.UnitTests.Billing.Queries;

public class GetSubscriptionsListQueryHandlerTests : IDisposable
{
    private readonly ApplicationDbContext _dbContext;
    private readonly GetSubscriptionsListQueryHandler _handler;

    public GetSubscriptionsListQueryHandlerTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new ApplicationDbContext(options);
        _handler = new GetSubscriptionsListQueryHandler(_dbContext);
    }

    [Fact]
    public async Task Should_Return_Empty_List_When_No_Subscriptions()
    {
        var result = await _handler.Handle(
            new GetSubscriptionsListQuery { PageNumber = 1, PageSize = 10 },
            CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.Data!.Items.Should().BeEmpty();
        result.Data.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task Should_Return_Paginated_Results()
    {
        var (plan, user1, user2, user3) = SeedThreeSubscriptions();
        await _dbContext.SaveChangesAsync();

        var result = await _handler.Handle(
            new GetSubscriptionsListQuery { PageNumber = 1, PageSize = 2 },
            CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.Data!.Items.Should().HaveCount(2);
        result.Data.TotalCount.Should().Be(3);
        result.Data.TotalPages.Should().Be(2);
        result.Data.HasNextPage.Should().BeTrue();
    }

    [Fact]
    public async Task Should_Filter_By_PlanId()
    {
        var plan1 = CreatePlan("Plan 1");
        var plan2 = CreatePlan("Plan 2");
        _dbContext.SubscriptionPlans.AddRange(plan1, plan2);
        var user1 = CreateUser("u1@test.com");
        var user2 = CreateUser("u2@test.com");
        _dbContext.Users.AddRange(user1, user2);
        _dbContext.UserSubscriptions.AddRange(
            CreateSubscription(plan1.Id, user1.Id, SubscriptionStatus.Active),
            CreateSubscription(plan2.Id, user2.Id, SubscriptionStatus.Active));
        await _dbContext.SaveChangesAsync();

        var result = await _handler.Handle(
            new GetSubscriptionsListQuery { PageNumber = 1, PageSize = 10, PlanIdFilter = plan1.Id },
            CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.Data!.Items.Should().HaveCount(1);
        result.Data.Items[0].PlanName.Should().Be("Plan 1");
    }

    [Fact]
    public async Task Should_Filter_By_Status()
    {
        var plan = CreatePlan("Pro");
        _dbContext.SubscriptionPlans.Add(plan);
        var user1 = CreateUser("s1@test.com");
        var user2 = CreateUser("s2@test.com");
        _dbContext.Users.AddRange(user1, user2);
        _dbContext.UserSubscriptions.AddRange(
            CreateSubscription(plan.Id, user1.Id, SubscriptionStatus.Active),
            CreateSubscription(plan.Id, user2.Id, SubscriptionStatus.Canceled));
        await _dbContext.SaveChangesAsync();

        var result = await _handler.Handle(
            new GetSubscriptionsListQuery { PageNumber = 1, PageSize = 10, StatusFilter = "Active" },
            CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.Data!.Items.Should().HaveCount(1);
        result.Data.Items[0].Status.Should().Be("Active");
    }

    private (SubscriptionPlan plan, ApplicationUser u1, ApplicationUser u2, ApplicationUser u3) SeedThreeSubscriptions()
    {
        var plan = CreatePlan("Pro");
        _dbContext.SubscriptionPlans.Add(plan);
        var u1 = CreateUser("a@test.com");
        var u2 = CreateUser("b@test.com");
        var u3 = CreateUser("c@test.com");
        _dbContext.Users.AddRange(u1, u2, u3);
        _dbContext.UserSubscriptions.AddRange(
            CreateSubscription(plan.Id, u1.Id, SubscriptionStatus.Active),
            CreateSubscription(plan.Id, u2.Id, SubscriptionStatus.Active),
            CreateSubscription(plan.Id, u3.Id, SubscriptionStatus.Trialing));
        return (plan, u1, u2, u3);
    }

    private static SubscriptionPlan CreatePlan(string name) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        MonthlyPrice = 9.99m,
        YearlyPrice = 99.99m,
        Status = PlanStatus.Active
    };

    private static ApplicationUser CreateUser(string email) => new()
    {
        Id = Guid.NewGuid(),
        UserName = email,
        Email = email,
        FirstName = "Test",
        LastName = "User"
    };

    private static UserSubscription CreateSubscription(Guid planId, Guid userId, SubscriptionStatus status) => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        PlanId = planId,
        Status = status,
        CurrentPeriodStart = DateTime.UtcNow,
        CurrentPeriodEnd = DateTime.UtcNow.AddMonths(1)
    };

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
