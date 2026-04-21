using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Seed.Application.Admin.Plans.Commands.CreatePlan;
using Seed.Application.Admin.Plans.Models;
using Seed.Application.Common.Interfaces;
using Seed.Application.Common.Models;
using Seed.Domain.Authorization;
using Seed.Infrastructure.Billing.Commands;
using Seed.Infrastructure.Persistence;

namespace Seed.UnitTests.Billing.Commands;

public class CreatePlanCommandHandlerTests : IDisposable
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IPaymentGateway _paymentGateway;
    private readonly IAuditService _auditService;
    private readonly CreatePlanCommandHandler _handler;

    public CreatePlanCommandHandlerTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new ApplicationDbContext(options);
        _paymentGateway = Substitute.For<IPaymentGateway>();
        _auditService = Substitute.For<IAuditService>();

        _paymentGateway.SyncPlanToProviderAsync(Arg.Any<SyncPlanRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ProductSyncResult("prod_123", "price_monthly_123", "price_yearly_123"));

        _handler = new CreatePlanCommandHandler(_dbContext, _paymentGateway, _auditService);
    }

    private static CreatePlanCommand CreateCommand(List<CreatePlanFeatureRequest>? features = null) => new(
        Name: "Pro Plan",
        Description: "Professional plan",
        MonthlyPrice: 9.99m,
        YearlyPrice: 99.99m,
        TrialDays: 14,
        IsFreeTier: false,
        IsDefault: false,
        IsPopular: true,
        SortOrder: 1,
        Features: features ?? [])
    {
        CurrentUserId = Guid.NewGuid(),
        IpAddress = "127.0.0.1",
        UserAgent = "TestAgent"
    };

    [Fact]
    public async Task Should_Create_Plan_And_Sync_To_Stripe()
    {
        var result = await _handler.Handle(CreateCommand(), CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.Data.Should().NotBeEmpty();

        var plan = await _dbContext.SubscriptionPlans.FindAsync(result.Data);
        plan.Should().NotBeNull();
        plan!.Name.Should().Be("Pro Plan");

        await _paymentGateway.Received(1).SyncPlanToProviderAsync(
            Arg.Any<SyncPlanRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_Create_Plan_With_Features()
    {
        var features = new List<CreatePlanFeatureRequest>
        {
            new("storage", "10 GB Storage", "10", 1),
            new("users", "5 Users", "5", 2)
        };

        var result = await _handler.Handle(CreateCommand(features), CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        var plan = await _dbContext.SubscriptionPlans
            .Include(p => p.Features)
            .FirstAsync(p => p.Id == result.Data);
        plan.Features.Should().HaveCount(2);
    }

    [Fact]
    public async Task Should_Set_Correct_Stripe_Ids_From_Sync_Result()
    {
        var result = await _handler.Handle(CreateCommand(), CancellationToken.None);

        var plan = await _dbContext.SubscriptionPlans.FindAsync(result.Data);
        plan!.StripeProductId.Should().Be("prod_123");
        plan.StripePriceIdMonthly.Should().Be("price_monthly_123");
        plan.StripePriceIdYearly.Should().Be("price_yearly_123");
    }

    [Fact]
    public async Task Should_Audit_Log_On_Creation()
    {
        var command = CreateCommand();
        await _handler.Handle(command, CancellationToken.None);

        await _auditService.Received(1).LogAsync(
            AuditActions.PlanCreated,
            entityType: "SubscriptionPlan",
            entityId: Arg.Any<string>(),
            details: Arg.Is<string>(d => d.Contains("Pro Plan")),
            userId: command.CurrentUserId,
            ipAddress: "127.0.0.1",
            userAgent: "TestAgent",
            cancellationToken: Arg.Any<CancellationToken>());
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
