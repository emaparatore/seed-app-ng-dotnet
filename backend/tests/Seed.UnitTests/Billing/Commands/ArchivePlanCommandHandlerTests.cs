using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Seed.Application.Admin.Plans.Commands.ArchivePlan;
using Seed.Application.Common.Interfaces;
using Seed.Domain.Authorization;
using Seed.Domain.Entities;
using Seed.Domain.Enums;
using Seed.Infrastructure.Billing.Commands;
using Seed.Infrastructure.Persistence;

namespace Seed.UnitTests.Billing.Commands;

public class ArchivePlanCommandHandlerTests : IDisposable
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IAuditService _auditService;
    private readonly ArchivePlanCommandHandler _handler;

    private static readonly Guid TestPlanId = Guid.NewGuid();
    private static readonly Guid TestUserId = Guid.NewGuid();

    public ArchivePlanCommandHandlerTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new ApplicationDbContext(options);
        _auditService = Substitute.For<IAuditService>();
        _handler = new ArchivePlanCommandHandler(_dbContext, _auditService);
    }

    private ArchivePlanCommand CreateCommand(Guid? planId = null) => new(planId ?? TestPlanId)
    {
        CurrentUserId = TestUserId,
        IpAddress = "127.0.0.1",
        UserAgent = "TestAgent"
    };

    private void SeedPlan()
    {
        _dbContext.SubscriptionPlans.Add(new SubscriptionPlan
        {
            Id = TestPlanId,
            Name = "Pro Plan",
            MonthlyPrice = 9.99m,
            YearlyPrice = 99.99m,
            Status = PlanStatus.Active
        });
        _dbContext.SaveChanges();
    }

    [Fact]
    public async Task Should_Set_Status_To_Archived()
    {
        SeedPlan();

        var result = await _handler.Handle(CreateCommand(), CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        var plan = await _dbContext.SubscriptionPlans.FindAsync(TestPlanId);
        plan!.Status.Should().Be(PlanStatus.Archived);
    }

    [Fact]
    public async Task Should_Return_Failure_If_Plan_Not_Found()
    {
        var result = await _handler.Handle(CreateCommand(Guid.NewGuid()), CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("Plan not found.");
    }

    [Fact]
    public async Task Should_Audit_Log_On_Archive()
    {
        SeedPlan();

        await _handler.Handle(CreateCommand(), CancellationToken.None);

        await _auditService.Received(1).LogAsync(
            AuditActions.PlanArchived,
            entityType: "SubscriptionPlan",
            entityId: TestPlanId.ToString(),
            details: Arg.Is<string>(d => d.Contains("Pro Plan")),
            userId: TestUserId,
            ipAddress: "127.0.0.1",
            userAgent: "TestAgent",
            cancellationToken: Arg.Any<CancellationToken>());
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
