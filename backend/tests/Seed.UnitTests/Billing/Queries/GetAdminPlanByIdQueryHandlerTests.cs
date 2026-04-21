using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Seed.Application.Admin.Plans.Queries.GetAdminPlanById;
using Seed.Domain.Entities;
using Seed.Domain.Enums;
using Seed.Infrastructure.Billing.Queries;
using Seed.Infrastructure.Persistence;

namespace Seed.UnitTests.Billing.Queries;

public class GetAdminPlanByIdQueryHandlerTests : IDisposable
{
    private readonly ApplicationDbContext _dbContext;
    private readonly GetAdminPlanByIdQueryHandler _handler;

    public GetAdminPlanByIdQueryHandlerTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new ApplicationDbContext(options);
        _handler = new GetAdminPlanByIdQueryHandler(_dbContext);
    }

    [Fact]
    public async Task Should_Return_Plan_With_Features()
    {
        var planId = Guid.NewGuid();
        var plan = new SubscriptionPlan
        {
            Id = planId,
            Name = "Pro",
            MonthlyPrice = 9.99m,
            YearlyPrice = 99.99m,
            Status = PlanStatus.Active,
            Features = new List<PlanFeature>
            {
                new() { Id = Guid.NewGuid(), Key = "storage", Description = "10 GB", SortOrder = 1 }
            }
        };
        _dbContext.SubscriptionPlans.Add(plan);
        await _dbContext.SaveChangesAsync();

        var result = await _handler.Handle(new GetAdminPlanByIdQuery(planId), CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.Data!.Name.Should().Be("Pro");
        result.Data.Features.Should().HaveCount(1);
        result.Data.Features[0].Key.Should().Be("storage");
    }

    [Fact]
    public async Task Should_Return_Failure_If_Not_Found()
    {
        var result = await _handler.Handle(new GetAdminPlanByIdQuery(Guid.NewGuid()), CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("Plan not found.");
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
