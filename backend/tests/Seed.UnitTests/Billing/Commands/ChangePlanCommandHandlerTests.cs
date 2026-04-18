using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Seed.Application.Billing.Commands.ChangePlan;
using Seed.Application.Common.Interfaces;
using Seed.Application.Common.Models;
using Seed.Domain.Entities;
using Seed.Domain.Enums;
using Seed.Infrastructure.Billing.Commands;
using Seed.Infrastructure.Persistence;

namespace Seed.UnitTests.Billing.Commands;

public class ChangePlanCommandHandlerTests : IDisposable
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IPaymentGateway _paymentGateway;
    private readonly IAuditService _auditService;
    private readonly ILogger<ChangePlanCommandHandler> _logger;
    private readonly ChangePlanCommandHandler _handler;

    private static readonly Guid TestUserId = Guid.NewGuid();
    private const string ReturnUrl = "https://example.com/billing/subscription";

    public ChangePlanCommandHandlerTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new ApplicationDbContext(options);
        _paymentGateway = Substitute.For<IPaymentGateway>();
        _auditService = Substitute.For<IAuditService>();
        _logger = Substitute.For<ILogger<ChangePlanCommandHandler>>();

        _handler = new ChangePlanCommandHandler(_dbContext, _paymentGateway, _auditService, _logger);
    }

    [Fact]
    public async Task Should_Schedule_Downgrade_When_Target_MonthlyEquivalent_Is_Lower()
    {
        var currentPlan = CreatePlan("Current", monthlyPrice: 100m, yearlyPrice: 1200m, "price_cur_m", "price_cur_y");
        var targetPlan = CreatePlan("Target", monthlyPrice: 120m, yearlyPrice: 900m, "price_tar_m", "price_tar_y");

        var subscription = CreateSubscription(currentPlan.Id);
        _dbContext.SubscriptionPlans.AddRange(currentPlan, targetPlan);
        _dbContext.UserSubscriptions.Add(subscription);
        await _dbContext.SaveChangesAsync();

        _paymentGateway.GetSubscriptionAsync(subscription.StripeSubscriptionId!, Arg.Any<CancellationToken>())
            .Returns(new SubscriptionDetails(
                SubscriptionId: subscription.StripeSubscriptionId!,
                CustomerId: subscription.StripeCustomerId!,
                Status: "active",
                PriceId: currentPlan.StripePriceIdMonthly!,
                CurrentPeriodStart: subscription.CurrentPeriodStart,
                CurrentPeriodEnd: subscription.CurrentPeriodEnd,
                TrialEnd: null,
                CancelAtPeriodEnd: false));

        _paymentGateway.ScheduleSubscriptionDowngradeAsync(subscription.StripeSubscriptionId!, targetPlan.StripePriceIdYearly!, Arg.Any<CancellationToken>())
            .Returns(new ScheduledDowngradeResult("sched_test_123", DateTime.UtcNow.AddMonths(1)));

        var command = new ChangePlanCommand(targetPlan.Id, BillingInterval.Yearly, ReturnUrl) { UserId = TestUserId };

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.Data!.RedirectUrl.Should().BeNull();
        await _paymentGateway.Received(1).ScheduleSubscriptionDowngradeAsync(
            subscription.StripeSubscriptionId!,
            targetPlan.StripePriceIdYearly!,
            Arg.Any<CancellationToken>());
        await _paymentGateway.DidNotReceive().CreateUpgradePortalSessionAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());

        var updated = await _dbContext.UserSubscriptions.FindAsync(subscription.Id);
        updated!.PlanId.Should().Be(currentPlan.Id);
        updated.ScheduledPlanId.Should().Be(targetPlan.Id);
    }

    [Fact]
    public async Task Should_Update_Subscription_Immediately_When_Target_MonthlyEquivalent_Is_Higher()
    {
        var currentPlan = CreatePlan("Current", monthlyPrice: 100m, yearlyPrice: 840m, "price_cur_m", "price_cur_y");
        var targetPlan = CreatePlan("Target", monthlyPrice: 90m, yearlyPrice: 960m, "price_tar_m", "price_tar_y");

        var subscription = CreateSubscription(currentPlan.Id);
        _dbContext.SubscriptionPlans.AddRange(currentPlan, targetPlan);
        _dbContext.UserSubscriptions.Add(subscription);
        await _dbContext.SaveChangesAsync();

        _paymentGateway.GetSubscriptionAsync(subscription.StripeSubscriptionId!, Arg.Any<CancellationToken>())
            .Returns(new SubscriptionDetails(
                SubscriptionId: subscription.StripeSubscriptionId!,
                CustomerId: subscription.StripeCustomerId!,
                Status: "active",
                PriceId: currentPlan.StripePriceIdYearly!,
                CurrentPeriodStart: subscription.CurrentPeriodStart,
                CurrentPeriodEnd: subscription.CurrentPeriodEnd,
                TrialEnd: null,
                CancelAtPeriodEnd: false));

        var now = DateTime.UtcNow;
        _paymentGateway.UpdateSubscriptionPriceAsync(
                subscription.StripeSubscriptionId!,
                targetPlan.StripePriceIdYearly!,
                Arg.Any<CancellationToken>())
            .Returns(new SubscriptionDetails(
                SubscriptionId: subscription.StripeSubscriptionId!,
                CustomerId: subscription.StripeCustomerId!,
                Status: "active",
                PriceId: targetPlan.StripePriceIdYearly!,
                CurrentPeriodStart: now,
                CurrentPeriodEnd: now.AddYears(1),
                TrialEnd: null,
                CancelAtPeriodEnd: false));

        var command = new ChangePlanCommand(targetPlan.Id, BillingInterval.Yearly, ReturnUrl) { UserId = TestUserId };

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.Data!.RedirectUrl.Should().BeNull("upgrade should update immediately with no portal redirect");
        await _paymentGateway.Received(1).UpdateSubscriptionPriceAsync(
            subscription.StripeSubscriptionId!,
            targetPlan.StripePriceIdYearly!,
            Arg.Any<CancellationToken>());
        await _paymentGateway.DidNotReceive().ScheduleSubscriptionDowngradeAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
        await _paymentGateway.DidNotReceive().CreateUpgradePortalSessionAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());

        // Plan should be updated immediately in local DB
        var updated = await _dbContext.UserSubscriptions.FindAsync(subscription.Id);
        updated!.PlanId.Should().Be(targetPlan.Id, "upgrade updates local plan immediately");
    }

    [Fact]
    public async Task Should_Fail_When_Request_Is_Same_Plan_And_Same_BillingInterval()
    {
        var currentPlan = CreatePlan("Current", monthlyPrice: 100m, yearlyPrice: 1000m, "price_cur_m", "price_cur_y");
        var subscription = CreateSubscription(currentPlan.Id);

        _dbContext.SubscriptionPlans.Add(currentPlan);
        _dbContext.UserSubscriptions.Add(subscription);
        await _dbContext.SaveChangesAsync();

        _paymentGateway.GetSubscriptionAsync(subscription.StripeSubscriptionId!, Arg.Any<CancellationToken>())
            .Returns(new SubscriptionDetails(
                SubscriptionId: subscription.StripeSubscriptionId!,
                CustomerId: subscription.StripeCustomerId!,
                Status: "active",
                PriceId: currentPlan.StripePriceIdMonthly!,
                CurrentPeriodStart: subscription.CurrentPeriodStart,
                CurrentPeriodEnd: subscription.CurrentPeriodEnd,
                TrialEnd: null,
                CancelAtPeriodEnd: false));

        var command = new ChangePlanCommand(currentPlan.Id, BillingInterval.Monthly, ReturnUrl) { UserId = TestUserId };

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("You are already on this plan and billing interval.");
        await _paymentGateway.DidNotReceive().CreateUpgradePortalSessionAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
        await _paymentGateway.DidNotReceive().ScheduleSubscriptionDowngradeAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    private static SubscriptionPlan CreatePlan(
        string name,
        decimal monthlyPrice,
        decimal yearlyPrice,
        string monthlyPriceId,
        string yearlyPriceId)
    {
        return new SubscriptionPlan
        {
            Id = Guid.NewGuid(),
            Name = name,
            MonthlyPrice = monthlyPrice,
            YearlyPrice = yearlyPrice,
            StripePriceIdMonthly = monthlyPriceId,
            StripePriceIdYearly = yearlyPriceId,
            Status = PlanStatus.Active,
            IsFreeTier = false,
        };
    }

    private static UserSubscription CreateSubscription(Guid planId)
    {
        return new UserSubscription
        {
            Id = Guid.NewGuid(),
            UserId = TestUserId,
            PlanId = planId,
            Status = SubscriptionStatus.Active,
            StripeSubscriptionId = "sub_test_123",
            StripeCustomerId = "cus_test_123",
            CurrentPeriodStart = DateTime.UtcNow.AddDays(-5),
            CurrentPeriodEnd = DateTime.UtcNow.AddDays(25),
        };
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
