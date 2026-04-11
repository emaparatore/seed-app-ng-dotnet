using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Seed.Application.Admin.Plans.Commands.UpdatePlan;
using Seed.Application.Admin.Plans.Models;
using Seed.Application.Common.Interfaces;
using Seed.Application.Common.Models;
using Seed.Domain.Authorization;
using Seed.Domain.Entities;
using Seed.Infrastructure.Billing.Commands;
using Seed.Infrastructure.Persistence;

namespace Seed.UnitTests.Billing.Commands;

public class UpdatePlanCommandHandlerTests : IDisposable
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IPaymentGateway _paymentGateway;
    private readonly IAuditService _auditService;
    private readonly UpdatePlanCommandHandler _handler;

    private static readonly Guid TestPlanId = Guid.NewGuid();

    public UpdatePlanCommandHandlerTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new ApplicationDbContext(options);
        _paymentGateway = Substitute.For<IPaymentGateway>();
        _auditService = Substitute.For<IAuditService>();

        _paymentGateway.SyncPlanToProviderAsync(Arg.Any<SyncPlanRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ProductSyncResult("prod_123", "price_monthly_new", "price_yearly_new"));

        _handler = new UpdatePlanCommandHandler(_dbContext, _paymentGateway, _auditService);
    }

    private SubscriptionPlan SeedPlan(List<PlanFeature>? features = null)
    {
        var plan = new SubscriptionPlan
        {
            Id = TestPlanId,
            Name = "Old Name",
            MonthlyPrice = 9.99m,
            YearlyPrice = 99.99m,
            SortOrder = 1,
            Features = features ?? []
        };
        _dbContext.SubscriptionPlans.Add(plan);
        _dbContext.SaveChanges();
        return plan;
    }

    private UpdatePlanCommand CreateCommand(List<CreatePlanFeatureRequest>? features = null) => new(
        Name: "Updated Plan",
        Description: "Updated description",
        MonthlyPrice: 19.99m,
        YearlyPrice: 199.99m,
        TrialDays: 7,
        IsFreeTier: false,
        IsDefault: false,
        IsPopular: true,
        SortOrder: 2,
        Features: features ?? [])
    {
        PlanId = TestPlanId,
        CurrentUserId = Guid.NewGuid(),
        IpAddress = "127.0.0.1",
        UserAgent = "TestAgent"
    };

    [Fact]
    public async Task Should_Update_Plan_Metadata()
    {
        SeedPlan();

        var result = await _handler.Handle(CreateCommand(), CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        var plan = await _dbContext.SubscriptionPlans.FindAsync(TestPlanId);
        plan!.Name.Should().Be("Updated Plan");
        plan.MonthlyPrice.Should().Be(19.99m);
    }

    [Fact]
    public async Task Should_Add_New_Features()
    {
        SeedPlan();
        var features = new List<CreatePlanFeatureRequest>
        {
            new("storage", "10 GB", "10", 1)
        };

        var result = await _handler.Handle(CreateCommand(features), CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        var plan = await _dbContext.SubscriptionPlans
            .Include(p => p.Features)
            .FirstAsync(p => p.Id == TestPlanId);
        plan.Features.Should().HaveCount(1);
        plan.Features.First().Key.Should().Be("storage");
    }

    [Fact]
    public async Task Should_Remove_Deleted_Features()
    {
        var existingFeatures = new List<PlanFeature>
        {
            new() { Id = Guid.NewGuid(), Key = "old_feature", Description = "Old", SortOrder = 1 }
        };
        SeedPlan(existingFeatures);

        var result = await _handler.Handle(CreateCommand([]), CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        var plan = await _dbContext.SubscriptionPlans
            .Include(p => p.Features)
            .FirstAsync(p => p.Id == TestPlanId);
        plan.Features.Should().BeEmpty();
    }

    [Fact]
    public async Task Should_Sync_To_Stripe_On_Update()
    {
        SeedPlan();

        await _handler.Handle(CreateCommand(), CancellationToken.None);

        await _paymentGateway.Received(1).SyncPlanToProviderAsync(
            Arg.Any<SyncPlanRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_Return_Failure_If_Plan_Not_Found()
    {
        var command = CreateCommand() with { PlanId = Guid.NewGuid() };
        var result = await _handler.Handle(command, CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("Plan not found.");
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
