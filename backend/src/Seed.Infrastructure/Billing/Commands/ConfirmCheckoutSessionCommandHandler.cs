using MediatR;
using Microsoft.EntityFrameworkCore;
using Seed.Application.Billing.Commands.ConfirmCheckoutSession;
using Seed.Application.Billing.Models;
using Seed.Application.Common;
using Seed.Application.Common.Interfaces;
using Seed.Application.Common.Models;
using Seed.Domain.Authorization;
using Seed.Domain.Entities;
using Seed.Domain.Enums;
using Seed.Infrastructure.Persistence;
using Seed.Infrastructure.Services.Payments;

namespace Seed.Infrastructure.Billing.Commands;

public sealed class ConfirmCheckoutSessionCommandHandler(
    ApplicationDbContext dbContext,
    IPaymentGateway paymentGateway,
    IAuditService auditService)
    : IRequestHandler<ConfirmCheckoutSessionCommand, Result<CheckoutConfirmationResponse>>
{
    public async Task<Result<CheckoutConfirmationResponse>> Handle(
        ConfirmCheckoutSessionCommand request, CancellationToken cancellationToken)
    {
        var attempt = await dbContext.CheckoutSessionAttempts
            .FirstOrDefaultAsync(a => a.StripeSessionId == request.SessionId && a.UserId == request.UserId, cancellationToken);

        if (attempt?.Status == CheckoutSessionAttemptStatus.Completed)
            return Result<CheckoutConfirmationResponse>.Success(new CheckoutConfirmationResponse(true, "already_confirmed"));

        var session = await paymentGateway.GetCheckoutSessionAsync(request.SessionId, cancellationToken);
        if (session is null)
            return await FailAsync(request, attempt, "Stripe checkout session not found.", cancellationToken);

        if (!string.Equals(session.Status, "complete", StringComparison.OrdinalIgnoreCase)
            || (!string.Equals(session.PaymentStatus, "paid", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(session.PaymentStatus, "no_payment_required", StringComparison.OrdinalIgnoreCase)))
        {
            return await FailAsync(request, attempt,
                $"Checkout session is not completed yet (status={session.Status}, paymentStatus={session.PaymentStatus}).",
                cancellationToken);
        }

        var metadataUserId = session.Metadata.GetValueOrDefault("userId");
        if (string.IsNullOrWhiteSpace(metadataUserId)
            || !Guid.TryParse(metadataUserId, out var metadataUserGuid)
            || metadataUserGuid != request.UserId)
        {
            return await FailAsync(request, attempt,
                "Checkout session does not belong to the authenticated user.",
                cancellationToken);
        }

        var planId = await ResolvePlanIdAsync(session, cancellationToken);
        if (planId is null)
            return await FailAsync(request, attempt, "Unable to resolve subscription plan from checkout session.", cancellationToken);

        if (string.IsNullOrWhiteSpace(session.SubscriptionId))
            return await FailAsync(request, attempt, "Checkout session does not contain a subscription id.", cancellationToken);

        var stripeSubscription = await paymentGateway.GetSubscriptionAsync(session.SubscriptionId, cancellationToken);
        var currentPeriodStart = stripeSubscription?.CurrentPeriodStart ?? DateTime.UtcNow;
        var currentPeriodEnd = stripeSubscription?.CurrentPeriodEnd ?? DateTime.UtcNow.AddMonths(1);
        var status = StripeWebhookEventHandler.MapStripeStatus(stripeSubscription?.Status ?? "active");

        var existingSubscription = await dbContext.UserSubscriptions
            .FirstOrDefaultAsync(s => s.StripeSubscriptionId == session.SubscriptionId, cancellationToken);

        if (existingSubscription is null)
        {
            var activeSubscriptions = await dbContext.UserSubscriptions
                .Where(s => s.UserId == request.UserId
                    && (s.Status == SubscriptionStatus.Active || s.Status == SubscriptionStatus.Trialing)
                    && s.StripeSubscriptionId != session.SubscriptionId)
                .ToListAsync(cancellationToken);

            foreach (var active in activeSubscriptions)
            {
                active.Status = SubscriptionStatus.Canceled;
                active.CanceledAt = DateTime.UtcNow;
                active.UpdatedAt = DateTime.UtcNow;
            }

            existingSubscription = new UserSubscription
            {
                Id = Guid.NewGuid(),
                UserId = request.UserId,
                PlanId = planId.Value,
                Status = status,
                StripeSubscriptionId = session.SubscriptionId,
                StripeCustomerId = session.CustomerId,
                CurrentPeriodStart = currentPeriodStart,
                CurrentPeriodEnd = currentPeriodEnd,
                TrialEnd = stripeSubscription?.TrialEnd,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };
            dbContext.UserSubscriptions.Add(existingSubscription);
        }
        else
        {
            existingSubscription.PlanId = planId.Value;
            existingSubscription.Status = status;
            existingSubscription.StripeCustomerId = session.CustomerId;
            existingSubscription.CurrentPeriodStart = currentPeriodStart;
            existingSubscription.CurrentPeriodEnd = currentPeriodEnd;
            existingSubscription.TrialEnd = stripeSubscription?.TrialEnd;
            existingSubscription.UpdatedAt = DateTime.UtcNow;
        }

        if (attempt is null)
        {
            attempt = new CheckoutSessionAttempt
            {
                Id = Guid.NewGuid(),
                UserId = request.UserId,
                PlanId = planId.Value,
                StripeSessionId = session.SessionId,
                CreatedAt = DateTime.UtcNow,
            };
            dbContext.CheckoutSessionAttempts.Add(attempt);
        }

        attempt.Status = CheckoutSessionAttemptStatus.Completed;
        attempt.PlanId = planId.Value;
        attempt.StripeCustomerId = session.CustomerId;
        attempt.StripeSubscriptionId = session.SubscriptionId;
        attempt.CompletedAt = DateTime.UtcNow;
        attempt.FailureReason = null;
        attempt.UpdatedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);

        await auditService.LogAsync(
            AuditActions.CheckoutSessionConfirmationSucceeded,
            entityType: "CheckoutSessionAttempt",
            entityId: attempt.Id.ToString(),
            details: $"Session {session.SessionId} confirmed and subscription {session.SubscriptionId} synchronized",
            userId: request.UserId,
            ipAddress: request.IpAddress,
            userAgent: request.UserAgent,
            cancellationToken: cancellationToken);

        return Result<CheckoutConfirmationResponse>.Success(new CheckoutConfirmationResponse(true, "confirmed"));
    }

    private async Task<Guid?> ResolvePlanIdAsync(CheckoutSessionDetails session, CancellationToken cancellationToken)
    {
        var metadataPlanId = session.Metadata.GetValueOrDefault("planId");
        if (!string.IsNullOrWhiteSpace(metadataPlanId)
            && Guid.TryParse(metadataPlanId, out var planGuid)
            && await dbContext.SubscriptionPlans.AnyAsync(p => p.Id == planGuid, cancellationToken))
        {
            return planGuid;
        }

        if (string.IsNullOrWhiteSpace(session.SubscriptionId))
            return null;

        var stripeSubscription = await paymentGateway.GetSubscriptionAsync(session.SubscriptionId, cancellationToken);
        if (stripeSubscription is null)
            return null;

        var plan = await dbContext.SubscriptionPlans
            .AsNoTracking()
            .FirstOrDefaultAsync(
                p => p.StripePriceIdMonthly == stripeSubscription.PriceId
                     || p.StripePriceIdYearly == stripeSubscription.PriceId,
                cancellationToken);

        return plan?.Id;
    }

    private async Task<Result<CheckoutConfirmationResponse>> FailAsync(
        ConfirmCheckoutSessionCommand request,
        CheckoutSessionAttempt? attempt,
        string reason,
        CancellationToken cancellationToken)
    {
        if (attempt is not null)
        {
            attempt.Status = CheckoutSessionAttemptStatus.Failed;
            attempt.FailureReason = reason;
            attempt.UpdatedAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        await auditService.LogAsync(
            AuditActions.CheckoutSessionConfirmationFailed,
            entityType: "CheckoutSessionAttempt",
            entityId: attempt?.Id.ToString(),
            details: reason,
            userId: request.UserId,
            ipAddress: request.IpAddress,
            userAgent: request.UserAgent,
            cancellationToken: cancellationToken);

        return Result<CheckoutConfirmationResponse>.Failure(reason);
    }
}
