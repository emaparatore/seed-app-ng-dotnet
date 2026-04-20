using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Seed.Application.Billing.Queries.GetPlans;
using Seed.Domain.Entities;
using Seed.Domain.Enums;
using Seed.Infrastructure.Billing.Queries;
using Seed.Infrastructure.Persistence;

namespace Seed.UnitTests.Billing.Queries;

public class GetPlansQueryHandlerTests : IDisposable
{
    private readonly ApplicationDbContext _dbContext;
    private readonly GetPlansQueryHandler _handler;

    public GetPlansQueryHandlerTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new ApplicationDbContext(options);
        _handler = new GetPlansQueryHandler(_dbContext);
    }

    [Fact]
    public async Task Should_Return_Only_Active_Plans()
    {
        _dbContext.SubscriptionPlans.AddRange(
            CreatePlan("Active Plan", PlanStatus.Active),
            CreatePlan("Inactive Plan", PlanStatus.Inactive),
            CreatePlan("Archived Plan", PlanStatus.Archived));
        await _dbContext.SaveChangesAsync();

        var result = await _handler.Handle(new GetPlansQuery(), CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.Data.Should().HaveCount(1);
        result.Data![0].Name.Should().Be("Active Plan");
    }

    [Fact]
    public async Task Should_Return_Plans_Ordered_By_SortOrder()
    {
        _dbContext.SubscriptionPlans.AddRange(
            CreatePlan("Third", PlanStatus.Active, sortOrder: 3),
            CreatePlan("First", PlanStatus.Active, sortOrder: 1),
            CreatePlan("Second", PlanStatus.Active, sortOrder: 2));
        await _dbContext.SaveChangesAsync();

        var result = await _handler.Handle(new GetPlansQuery(), CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.Data.Should().HaveCount(3);
        result.Data![0].Name.Should().Be("First");
        result.Data[1].Name.Should().Be("Second");
        result.Data[2].Name.Should().Be("Third");
    }

    [Fact]
    public async Task Should_Include_Features_In_Response()
    {
        var plan = CreatePlan("Pro", PlanStatus.Active);
        plan.Features = new List<PlanFeature>
        {
            new() { Id = Guid.NewGuid(), Key = "storage", Description = "10 GB Storage", LimitValue = "10", SortOrder = 1 },
            new() { Id = Guid.NewGuid(), Key = "users", Description = "5 Users", LimitValue = "5", SortOrder = 2 }
        };
        _dbContext.SubscriptionPlans.Add(plan);
        await _dbContext.SaveChangesAsync();

        var result = await _handler.Handle(new GetPlansQuery(), CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.Data.Should().HaveCount(1);
        result.Data![0].Features.Should().HaveCount(2);
        result.Data[0].Features[0].Key.Should().Be("storage");
        result.Data[0].Features[1].Key.Should().Be("users");
    }

    [Fact]
    public async Task Should_Return_Empty_List_When_No_Active_Plans()
    {
        _dbContext.SubscriptionPlans.Add(CreatePlan("Inactive", PlanStatus.Inactive));
        await _dbContext.SaveChangesAsync();

        var result = await _handler.Handle(new GetPlansQuery(), CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.Data.Should().BeEmpty();
    }

    private static SubscriptionPlan CreatePlan(string name, PlanStatus status, int sortOrder = 0)
    {
        return new SubscriptionPlan
        {
            Id = Guid.NewGuid(),
            Name = name,
            MonthlyPrice = 9.99m,
            YearlyPrice = 99.99m,
            Status = status,
            SortOrder = sortOrder
        };
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
