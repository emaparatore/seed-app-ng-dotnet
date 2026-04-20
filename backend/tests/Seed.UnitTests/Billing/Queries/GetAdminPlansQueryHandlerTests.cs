using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Seed.Application.Admin.Plans.Queries.GetAdminPlans;
using Seed.Domain.Entities;
using Seed.Domain.Enums;
using Seed.Infrastructure.Billing.Queries;
using Seed.Infrastructure.Persistence;

namespace Seed.UnitTests.Billing.Queries;

public class GetAdminPlansQueryHandlerTests : IDisposable
{
    private readonly ApplicationDbContext _dbContext;
    private readonly GetAdminPlansQueryHandler _handler;

    public GetAdminPlansQueryHandlerTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new ApplicationDbContext(options);
        _handler = new GetAdminPlansQueryHandler(_dbContext);
    }

    [Fact]
    public async Task Should_Return_All_Plans_Including_Inactive_And_Archived()
    {
        _dbContext.SubscriptionPlans.AddRange(
            CreatePlan("Active", PlanStatus.Active),
            CreatePlan("Inactive", PlanStatus.Inactive),
            CreatePlan("Archived", PlanStatus.Archived));
        await _dbContext.SaveChangesAsync();

        var result = await _handler.Handle(new GetAdminPlansQuery(), CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.Data.Should().HaveCount(3);
    }

    [Fact]
    public async Task Should_Include_Subscriber_Count()
    {
        var plan = CreatePlan("Pro", PlanStatus.Active);
        _dbContext.SubscriptionPlans.Add(plan);
        _dbContext.UserSubscriptions.AddRange(
            new UserSubscription
            {
                Id = Guid.NewGuid(), UserId = Guid.NewGuid(), PlanId = plan.Id,
                Status = SubscriptionStatus.Active,
                CurrentPeriodStart = DateTime.UtcNow, CurrentPeriodEnd = DateTime.UtcNow.AddMonths(1)
            },
            new UserSubscription
            {
                Id = Guid.NewGuid(), UserId = Guid.NewGuid(), PlanId = plan.Id,
                Status = SubscriptionStatus.Trialing,
                CurrentPeriodStart = DateTime.UtcNow, CurrentPeriodEnd = DateTime.UtcNow.AddMonths(1)
            },
            new UserSubscription
            {
                Id = Guid.NewGuid(), UserId = Guid.NewGuid(), PlanId = plan.Id,
                Status = SubscriptionStatus.Canceled,
                CurrentPeriodStart = DateTime.UtcNow, CurrentPeriodEnd = DateTime.UtcNow.AddMonths(1)
            });
        await _dbContext.SaveChangesAsync();

        var result = await _handler.Handle(new GetAdminPlansQuery(), CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.Data![0].SubscriberCount.Should().Be(2);
    }

    [Fact]
    public async Task Should_Order_By_SortOrder()
    {
        _dbContext.SubscriptionPlans.AddRange(
            CreatePlan("Third", PlanStatus.Active, 3),
            CreatePlan("First", PlanStatus.Active, 1),
            CreatePlan("Second", PlanStatus.Active, 2));
        await _dbContext.SaveChangesAsync();

        var result = await _handler.Handle(new GetAdminPlansQuery(), CancellationToken.None);

        result.Data![0].Name.Should().Be("First");
        result.Data[1].Name.Should().Be("Second");
        result.Data[2].Name.Should().Be("Third");
    }

    private static SubscriptionPlan CreatePlan(string name, PlanStatus status, int sortOrder = 0) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        MonthlyPrice = 9.99m,
        YearlyPrice = 99.99m,
        Status = status,
        SortOrder = sortOrder
    };

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
