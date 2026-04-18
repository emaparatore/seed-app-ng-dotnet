using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Seed.Application.Billing.Commands.ChangePlan;
using Seed.Application.Billing.Models;
using Seed.Application.Common;
using Seed.Application.Common.Interfaces;
using Seed.Domain.Authorization;
using Seed.Domain.Enums;
using Seed.Infrastructure.Persistence;

namespace Seed.Infrastructure.Billing.Commands;

public sealed class ChangePlanCommandHandler(
    ApplicationDbContext dbContext,
    IPaymentGateway paymentGateway,
    IAuditService auditService,
    ILogger<ChangePlanCommandHandler> logger)
    : IRequestHandler<ChangePlanCommand, Result<ChangePlanResult>>
{
    public async Task<Result<ChangePlanResult>> Handle(
        ChangePlanCommand request, CancellationToken cancellationToken)
    {
        var subscription = await dbContext.UserSubscriptions
            .Include(s => s.Plan)
            .Where(s => s.UserId == request.UserId
                && (s.Status == SubscriptionStatus.Active || s.Status == SubscriptionStatus.Trialing))
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (subscription is null)
            return Result<ChangePlanResult>.Failure("No active subscription found.");

        if (string.IsNullOrWhiteSpace(subscription.StripeSubscriptionId))
            return Result<ChangePlanResult>.Failure("Subscription has no payment provider reference.");

        if (string.IsNullOrWhiteSpace(subscription.StripeCustomerId))
            return Result<ChangePlanResult>.Failure("Subscription has no customer reference.");

        var currentBillingInterval = await ResolveCurrentBillingIntervalAsync(subscription, cancellationToken);

        var newPlan = await dbContext.SubscriptionPlans
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == request.PlanId, cancellationToken);

        if (newPlan is null)
            return Result<ChangePlanResult>.Failure("Plan not found.");

        if (newPlan.Status != PlanStatus.Active)
            return Result<ChangePlanResult>.Failure("Plan is not active.");

        if (newPlan.IsFreeTier)
            return Result<ChangePlanResult>.Failure("Cannot change to a free tier plan. Cancel your subscription instead.");

        if (subscription.PlanId == request.PlanId && request.BillingInterval == currentBillingInterval)
            return Result<ChangePlanResult>.Failure("You are already on this plan and billing interval.");

        var newPriceId = request.BillingInterval == BillingInterval.Monthly
            ? newPlan.StripePriceIdMonthly
            : newPlan.StripePriceIdYearly;

        if (string.IsNullOrWhiteSpace(newPriceId))
            return Result<ChangePlanResult>.Failure($"No price configured for {request.BillingInterval} billing interval.");

        var oldPlanId = subscription.PlanId;
        var currentPeriodPrice = currentBillingInterval == BillingInterval.Monthly
            ? subscription.Plan.MonthlyPrice
            : subscription.Plan.YearlyPrice;
        var targetPeriodPrice = request.BillingInterval == BillingInterval.Monthly
            ? newPlan.MonthlyPrice
            : newPlan.YearlyPrice;

        var currentMonthlyEquivalent = ToMonthlyEquivalent(currentPeriodPrice, currentBillingInterval);
        var targetMonthlyEquivalent = ToMonthlyEquivalent(targetPeriodPrice, request.BillingInterval);
        var isDowngrade = targetMonthlyEquivalent < currentMonthlyEquivalent;
        var isUpgrade = targetMonthlyEquivalent > currentMonthlyEquivalent;

        // Cancel any existing scheduled downgrade before processing
        if (!string.IsNullOrWhiteSpace(subscription.StripeScheduleId))
        {
            await paymentGateway.CancelSubscriptionScheduleAsync(subscription.StripeScheduleId, cancellationToken);
            subscription.ScheduledPlanId = null;
            subscription.StripeScheduleId = null;
            subscription.UpdatedAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        string changeType;
        string? redirectUrl = null;

        if (isUpgrade || !isDowngrade)
        {
            // Upgrade/lateral change: update subscription immediately with proration invoice
            var updatedDetails = await paymentGateway.UpdateSubscriptionPriceAsync(
                subscription.StripeSubscriptionId,
                newPriceId,
                cancellationToken);

            subscription.PlanId = request.PlanId;
            subscription.CurrentPeriodStart = updatedDetails.CurrentPeriodStart;
            subscription.CurrentPeriodEnd = updatedDetails.CurrentPeriodEnd;
            subscription.UpdatedAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);

            changeType = isUpgrade ? "Upgrade (immediate)" : "Lateral change (immediate)";
        }
        else
        {
            // Downgrade: scheduled at end of current billing period
            var result = await paymentGateway.ScheduleSubscriptionDowngradeAsync(
                subscription.StripeSubscriptionId, newPriceId, cancellationToken);

            subscription.ScheduledPlanId = request.PlanId;
            subscription.StripeScheduleId = result.ScheduleId;
            subscription.UpdatedAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);

            changeType = "Downgrade (scheduled)";
        }

        await auditService.LogAsync(
            AuditActions.SubscriptionPlanChanged,
            entityType: "UserSubscription",
            entityId: subscription.Id.ToString(),
            details:
            $"{changeType} from plan {oldPlanId} ({currentBillingInterval}, {currentPeriodPrice:0.00} EUR) " +
            $"to {request.PlanId} ({request.BillingInterval}, {targetPeriodPrice:0.00} EUR). " +
            $"Monthly equivalent: {currentMonthlyEquivalent:0.00} -> {targetMonthlyEquivalent:0.00} EUR",
            userId: request.UserId,
            ipAddress: request.IpAddress,
            userAgent: request.UserAgent,
            cancellationToken: cancellationToken);

        logger.LogInformation(
            "Subscription {SubscriptionId} {ChangeType} from {OldPlan} ({OldInterval}, {OldMonthlyEq} EUR/mo) " +
            "to {NewPlan} ({NewInterval}, {NewMonthlyEq} EUR/mo) for user {UserId}",
            subscription.StripeSubscriptionId,
            changeType,
            oldPlanId,
            currentBillingInterval,
            currentMonthlyEquivalent,
            request.PlanId,
            request.BillingInterval,
            targetMonthlyEquivalent,
            request.UserId);

        return Result<ChangePlanResult>.Success(new ChangePlanResult(redirectUrl));
    }

    private async Task<BillingInterval> ResolveCurrentBillingIntervalAsync(
        Seed.Domain.Entities.UserSubscription subscription,
        CancellationToken cancellationToken)
    {
        var stripeSubscription = await paymentGateway.GetSubscriptionAsync(
            subscription.StripeSubscriptionId!,
            cancellationToken);

        if (!string.IsNullOrWhiteSpace(stripeSubscription?.PriceId))
        {
            if (string.Equals(stripeSubscription.PriceId, subscription.Plan.StripePriceIdMonthly, StringComparison.Ordinal))
            {
                return BillingInterval.Monthly;
            }

            if (string.Equals(stripeSubscription.PriceId, subscription.Plan.StripePriceIdYearly, StringComparison.Ordinal))
            {
                return BillingInterval.Yearly;
            }
        }

        var periodLength = subscription.CurrentPeriodEnd - subscription.CurrentPeriodStart;
        var inferred = periodLength.TotalDays >= 180 ? BillingInterval.Yearly : BillingInterval.Monthly;

        logger.LogWarning(
            "Unable to resolve current billing interval from Stripe price for subscription {SubscriptionId}. " +
            "Falling back to period-length inference ({InferredInterval}).",
            subscription.StripeSubscriptionId,
            inferred);

        return inferred;
    }

    private static decimal ToMonthlyEquivalent(decimal periodPrice, BillingInterval interval)
    {
        return interval == BillingInterval.Yearly ? periodPrice / 12m : periodPrice;
    }
}
