using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Seed.Application.Admin.Subscriptions.Queries.GetSubscriptionDetail;
using Seed.Domain.Entities;
using Seed.Domain.Enums;
using Seed.Infrastructure.Billing.Queries;
using Seed.Infrastructure.Persistence;

namespace Seed.UnitTests.Billing.Queries;

public class GetSubscriptionDetailQueryHandlerTests : IDisposable
{
    private readonly ApplicationDbContext _dbContext;
    private readonly GetSubscriptionDetailQueryHandler _handler;

    public GetSubscriptionDetailQueryHandlerTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new ApplicationDbContext(options);
        _handler = new GetSubscriptionDetailQueryHandler(_dbContext);
    }

    [Fact]
    public async Task Should_Return_Subscription_Detail_When_Found()
    {
        var plan = new SubscriptionPlan
        {
            Id = Guid.NewGuid(),
            Name = "Pro",
            MonthlyPrice = 9.99m,
            YearlyPrice = 99.99m,
            Status = PlanStatus.Active
        };
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = "detail@test.com",
            Email = "detail@test.com",
            FirstName = "Jane",
            LastName = "Doe"
        };
        var subscription = new UserSubscription
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            PlanId = plan.Id,
            Status = SubscriptionStatus.Active,
            StripeSubscriptionId = "sub_test",
            StripeCustomerId = "cus_test",
            CurrentPeriodStart = DateTime.UtcNow,
            CurrentPeriodEnd = DateTime.UtcNow.AddMonths(1)
        };
        _dbContext.SubscriptionPlans.Add(plan);
        _dbContext.Users.Add(user);
        _dbContext.UserSubscriptions.Add(subscription);
        await _dbContext.SaveChangesAsync();

        var result = await _handler.Handle(
            new GetSubscriptionDetailQuery(subscription.Id), CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.Data!.Id.Should().Be(subscription.Id);
        result.Data.UserEmail.Should().Be("detail@test.com");
        result.Data.UserFullName.Should().Be("Jane Doe");
        result.Data.PlanName.Should().Be("Pro");
        result.Data.Status.Should().Be("Active");
        result.Data.StripeSubscriptionId.Should().Be("sub_test");
    }

    [Fact]
    public async Task Should_Return_Failure_When_Not_Found()
    {
        var result = await _handler.Handle(
            new GetSubscriptionDetailQuery(Guid.NewGuid()), CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("Subscription not found");
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
