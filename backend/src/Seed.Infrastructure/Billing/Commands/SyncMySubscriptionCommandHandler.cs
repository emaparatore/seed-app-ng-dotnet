using MediatR;
using Microsoft.EntityFrameworkCore;
using Seed.Application.Billing.Commands.SyncMySubscription;
using Seed.Application.Billing.Models;
using Seed.Application.Common;
using Seed.Application.Common.Interfaces;
using Seed.Domain.Authorization;
using Seed.Domain.Enums;
using Seed.Infrastructure.Persistence;
using Seed.Infrastructure.Services.Payments;

namespace Seed.Infrastructure.Billing.Commands;

public sealed class SyncMySubscriptionCommandHandler(
    ApplicationDbContext dbContext,
    IPaymentGateway paymentGateway,
    IAuditService auditService)
    : IRequestHandler<SyncMySubscriptionCommand, Result<SyncSubscriptionResponse>>
{
    public async Task<Result<SyncSubscriptionResponse>> Handle(
        SyncMySubscriptionCommand request,
        CancellationToken cancellationToken)
    {
        var subscription = await dbContext.UserSubscriptions
            .Include(s => s.Plan)
            .Include(s => s.ScheduledPlan)
            .Where(s => s.UserId == request.UserId && s.StripeSubscriptionId != null)
            .OrderByDescending(s => s.UpdatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (subscription is null || string.IsNullOrWhiteSpace(subscription.StripeSubscriptionId))
            return Result<SyncSubscriptionResponse>.Failure("No Stripe subscription found to synchronize.");

        var stripeSubscription = await paymentGateway.GetSubscriptionAsync(subscription.StripeSubscriptionId, cancellationToken);
        if (stripeSubscription is null)
            return Result<SyncSubscriptionResponse>.Failure("Stripe subscription not found.");

        var status = StripeWebhookEventHandler.MapStripeStatus(stripeSubscription.Status);

        subscription.Status = status;
        subscription.CurrentPeriodStart = stripeSubscription.CurrentPeriodStart;
        subscription.CurrentPeriodEnd = stripeSubscription.CurrentPeriodEnd;
        subscription.TrialEnd = stripeSubscription.TrialEnd;

        var plan = await dbContext.SubscriptionPlans
            .FirstOrDefaultAsync(p => p.StripePriceIdMonthly == stripeSubscription.PriceId
                                      || p.StripePriceIdYearly == stripeSubscription.PriceId,
                cancellationToken);

        if (plan is not null && plan.Id != subscription.PlanId)
        {
            subscription.PlanId = plan.Id;
            subscription.ScheduledPlanId = null;
            subscription.StripeScheduleId = null;

            await auditService.LogAsync(
                AuditActions.SubscriptionPlanChanged,
                entityType: "UserSubscription",
                entityId: subscription.Id.ToString(),
                details: $"Plan synchronized from Stripe to '{plan.Name}'",
                userId: request.UserId,
                ipAddress: request.IpAddress,
                userAgent: request.UserAgent,
                cancellationToken: cancellationToken);
        }

        subscription.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result<SyncSubscriptionResponse>.Success(new SyncSubscriptionResponse(
            Synced: true,
            Status: subscription.Status.ToString(),
            PlanName: plan?.Name ?? subscription.Plan?.Name));
    }
}
