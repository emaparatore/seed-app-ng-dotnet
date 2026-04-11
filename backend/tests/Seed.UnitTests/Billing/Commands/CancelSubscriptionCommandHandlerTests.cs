using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Seed.Application.Billing.Commands.CancelSubscription;
using Seed.Application.Common.Interfaces;
using Seed.Domain.Authorization;
using Seed.Domain.Entities;
using Seed.Domain.Enums;
using Seed.Infrastructure.Billing.Commands;
using Seed.Infrastructure.Persistence;

namespace Seed.UnitTests.Billing.Commands;

public class CancelSubscriptionCommandHandlerTests : IDisposable
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IPaymentGateway _paymentGateway;
    private readonly IAuditService _auditService;
    private readonly CancelSubscriptionCommandHandler _handler;

    private static readonly Guid TestUserId = Guid.NewGuid();

    public CancelSubscriptionCommandHandlerTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new ApplicationDbContext(options);
        _paymentGateway = Substitute.For<IPaymentGateway>();
        _auditService = Substitute.For<IAuditService>();

        _handler = new CancelSubscriptionCommandHandler(_dbContext, _paymentGateway, _auditService);
    }

    private CancelSubscriptionCommand CreateCommand() => new()
    {
        UserId = TestUserId,
        IpAddress = "127.0.0.1",
        UserAgent = "TestAgent"
    };

    private UserSubscription CreateSubscription(
        SubscriptionStatus status = SubscriptionStatus.Active,
        string? stripeSubscriptionId = "sub_test_123")
    {
        return new UserSubscription
        {
            Id = Guid.NewGuid(),
            UserId = TestUserId,
            PlanId = Guid.NewGuid(),
            Status = status,
            StripeSubscriptionId = stripeSubscriptionId,
            StripeCustomerId = "cus_test_123",
            CurrentPeriodStart = DateTime.UtcNow,
            CurrentPeriodEnd = DateTime.UtcNow.AddMonths(1)
        };
    }

    [Fact]
    public async Task Should_Cancel_Subscription_And_Set_CanceledAt()
    {
        var subscription = CreateSubscription();
        _dbContext.UserSubscriptions.Add(subscription);
        await _dbContext.SaveChangesAsync();

        var result = await _handler.Handle(CreateCommand(), CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        var updated = await _dbContext.UserSubscriptions.FindAsync(subscription.Id);
        updated!.CanceledAt.Should().NotBeNull();
        updated.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Should_Call_PaymentGateway_CancelSubscriptionAsync()
    {
        var subscription = CreateSubscription();
        _dbContext.UserSubscriptions.Add(subscription);
        await _dbContext.SaveChangesAsync();

        await _handler.Handle(CreateCommand(), CancellationToken.None);

        await _paymentGateway.Received(1).CancelSubscriptionAsync(
            "sub_test_123", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_Log_Audit_Action()
    {
        var subscription = CreateSubscription();
        _dbContext.UserSubscriptions.Add(subscription);
        await _dbContext.SaveChangesAsync();

        await _handler.Handle(CreateCommand(), CancellationToken.None);

        await _auditService.Received(1).LogAsync(
            AuditActions.SubscriptionCanceled,
            entityType: "UserSubscription",
            entityId: subscription.Id.ToString(),
            details: Arg.Any<string>(),
            userId: TestUserId,
            ipAddress: "127.0.0.1",
            userAgent: "TestAgent",
            cancellationToken: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_Return_Failure_When_No_Active_Subscription()
    {
        var result = await _handler.Handle(CreateCommand(), CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("No active subscription found.");
    }

    [Fact]
    public async Task Should_Return_Failure_When_No_StripeSubscriptionId()
    {
        var subscription = CreateSubscription(stripeSubscriptionId: null);
        _dbContext.UserSubscriptions.Add(subscription);
        await _dbContext.SaveChangesAsync();

        var result = await _handler.Handle(CreateCommand(), CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("Subscription has no payment provider reference.");
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
