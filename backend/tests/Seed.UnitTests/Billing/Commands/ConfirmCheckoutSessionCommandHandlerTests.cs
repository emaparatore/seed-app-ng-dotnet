using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Seed.Application.Billing.Commands.ConfirmCheckoutSession;
using Seed.Application.Common.Interfaces;
using Seed.Application.Common.Models;
using Seed.Domain.Entities;
using Seed.Domain.Enums;
using Seed.Infrastructure.Billing.Commands;
using Seed.Infrastructure.Persistence;

namespace Seed.UnitTests.Billing.Commands;

public class ConfirmCheckoutSessionCommandHandlerTests : IDisposable
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IPaymentGateway _paymentGateway;
    private readonly IAuditService _auditService;
    private readonly ConfirmCheckoutSessionCommandHandler _handler;
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _planId = Guid.NewGuid();

    public ConfirmCheckoutSessionCommandHandlerTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new ApplicationDbContext(options);
        _paymentGateway = Substitute.For<IPaymentGateway>();
        _auditService = Substitute.For<IAuditService>();
        _handler = new ConfirmCheckoutSessionCommandHandler(_dbContext, _paymentGateway, _auditService);

        _dbContext.SubscriptionPlans.Add(new SubscriptionPlan
        {
            Id = _planId,
            Name = "Pro",
            Status = PlanStatus.Active,
            MonthlyPrice = 10,
            YearlyPrice = 100,
            StripePriceIdMonthly = "price_monthly_1",
            StripePriceIdYearly = "price_yearly_1"
        });

        _dbContext.CheckoutSessionAttempts.Add(new CheckoutSessionAttempt
        {
            Id = Guid.NewGuid(),
            UserId = _userId,
            PlanId = _planId,
            Status = CheckoutSessionAttemptStatus.Pending,
            StripeSessionId = "cs_test_123",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        _dbContext.SaveChanges();
    }

    [Fact]
    public async Task Should_Confirm_Checkout_And_Create_Subscription()
    {
        _paymentGateway.GetCheckoutSessionAsync("cs_test_123", Arg.Any<CancellationToken>())
            .Returns(new CheckoutSessionDetails(
                SessionId: "cs_test_123",
                Status: "complete",
                PaymentStatus: "paid",
                SubscriptionId: "sub_123",
                CustomerId: "cus_123",
                Metadata: new Dictionary<string, string>
                {
                    ["userId"] = _userId.ToString(),
                    ["planId"] = _planId.ToString()
                }));

        _paymentGateway.GetSubscriptionAsync("sub_123", Arg.Any<CancellationToken>())
            .Returns(new SubscriptionDetails(
                SubscriptionId: "sub_123",
                CustomerId: "cus_123",
                Status: "active",
                PriceId: "price_monthly_1",
                CurrentPeriodStart: DateTime.UtcNow,
                CurrentPeriodEnd: DateTime.UtcNow.AddMonths(1),
                TrialEnd: null,
                CancelAtPeriodEnd: false));

        var command = new ConfirmCheckoutSessionCommand("cs_test_123") { UserId = _userId };

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.Data!.Confirmed.Should().BeTrue();
        var subscription = await _dbContext.UserSubscriptions.FirstOrDefaultAsync();
        subscription.Should().NotBeNull();
        subscription!.StripeSubscriptionId.Should().Be("sub_123");

        var attempt = await _dbContext.CheckoutSessionAttempts.FirstAsync();
        attempt.Status.Should().Be(CheckoutSessionAttemptStatus.Completed);
    }

    [Fact]
    public async Task Should_Fail_When_Session_Belongs_To_Another_User()
    {
        _paymentGateway.GetCheckoutSessionAsync("cs_test_123", Arg.Any<CancellationToken>())
            .Returns(new CheckoutSessionDetails(
                SessionId: "cs_test_123",
                Status: "complete",
                PaymentStatus: "paid",
                SubscriptionId: "sub_123",
                CustomerId: "cus_123",
                Metadata: new Dictionary<string, string>
                {
                    ["userId"] = Guid.NewGuid().ToString(),
                    ["planId"] = _planId.ToString()
                }));

        var command = new ConfirmCheckoutSessionCommand("cs_test_123") { UserId = _userId };

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("Checkout session does not belong to the authenticated user.");

        var attempt = await _dbContext.CheckoutSessionAttempts.FirstAsync();
        attempt.Status.Should().Be(CheckoutSessionAttemptStatus.Failed);
    }

    [Fact]
    public async Task Should_Return_AlreadyConfirmed_When_Attempt_Already_Completed()
    {
        var attempt = await _dbContext.CheckoutSessionAttempts.FirstAsync();
        attempt.Status = CheckoutSessionAttemptStatus.Completed;
        await _dbContext.SaveChangesAsync();

        var command = new ConfirmCheckoutSessionCommand("cs_test_123") { UserId = _userId };

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.Data!.Status.Should().Be("already_confirmed");
        await _paymentGateway.DidNotReceive().GetCheckoutSessionAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
