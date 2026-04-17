using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Seed.Application.Billing.Commands.ChangePlan;
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
    : IRequestHandler<ChangePlanCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(
        ChangePlanCommand request, CancellationToken cancellationToken)
    {
        var subscription = await dbContext.UserSubscriptions
            .Include(s => s.Plan)
            .Where(s => s.UserId == request.UserId
                && (s.Status == SubscriptionStatus.Active || s.Status == SubscriptionStatus.Trialing))
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (subscription is null)
            return Result<bool>.Failure("No active subscription found.");

        if (string.IsNullOrWhiteSpace(subscription.StripeSubscriptionId))
            return Result<bool>.Failure("Subscription has no payment provider reference.");

        var newPlan = await dbContext.SubscriptionPlans
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == request.PlanId, cancellationToken);

        if (newPlan is null)
            return Result<bool>.Failure("Plan not found.");

        if (newPlan.Status != PlanStatus.Active)
            return Result<bool>.Failure("Plan is not active.");

        if (newPlan.IsFreeTier)
            return Result<bool>.Failure("Cannot change to a free tier plan. Cancel your subscription instead.");

        if (subscription.PlanId == request.PlanId)
            return Result<bool>.Failure("You are already on this plan.");

        var newPriceId = request.BillingInterval == BillingInterval.Monthly
            ? newPlan.StripePriceIdMonthly
            : newPlan.StripePriceIdYearly;

        if (string.IsNullOrWhiteSpace(newPriceId))
            return Result<bool>.Failure($"No price configured for {request.BillingInterval} billing interval.");

        var oldPlanId = subscription.PlanId;
        var isUpgrade = newPlan.MonthlyPrice > subscription.Plan.MonthlyPrice;

        // Cancel any existing scheduled downgrade before processing
        if (!string.IsNullOrWhiteSpace(subscription.StripeScheduleId))
        {
            await paymentGateway.CancelSubscriptionScheduleAsync(subscription.StripeScheduleId, cancellationToken);
            subscription.ScheduledPlanId = null;
            subscription.StripeScheduleId = null;
        }

        if (isUpgrade)
        {
            // Upgrade: immediate with proration
            var updated = await paymentGateway.UpdateSubscriptionPriceAsync(
                subscription.StripeSubscriptionId, newPriceId, cancellationToken);

            subscription.PlanId = request.PlanId;
            subscription.CurrentPeriodStart = updated.CurrentPeriodStart;
            subscription.CurrentPeriodEnd = updated.CurrentPeriodEnd;
        }
        else
        {
            // Downgrade: scheduled at end of current billing period
            var result = await paymentGateway.ScheduleSubscriptionDowngradeAsync(
                subscription.StripeSubscriptionId, newPriceId, cancellationToken);

            subscription.ScheduledPlanId = request.PlanId;
            subscription.StripeScheduleId = result.ScheduleId;
        }

        subscription.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        var changeType = isUpgrade ? "Upgrade" : "Downgrade (scheduled)";
        await auditService.LogAsync(
            AuditActions.SubscriptionPlanChanged,
            entityType: "UserSubscription",
            entityId: subscription.Id.ToString(),
            details: $"{changeType} from plan {oldPlanId} to {request.PlanId} ({request.BillingInterval})",
            userId: request.UserId,
            ipAddress: request.IpAddress,
            userAgent: request.UserAgent,
            cancellationToken: cancellationToken);

        logger.LogInformation(
            "Subscription {SubscriptionId} {ChangeType} from {OldPlan} to {NewPlan} for user {UserId}",
            subscription.StripeSubscriptionId, changeType, oldPlanId, request.PlanId, request.UserId);

        return Result<bool>.Success(true);
    }
}
