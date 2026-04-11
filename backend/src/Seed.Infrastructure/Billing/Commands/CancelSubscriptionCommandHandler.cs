using MediatR;
using Microsoft.EntityFrameworkCore;
using Seed.Application.Billing.Commands.CancelSubscription;
using Seed.Application.Common;
using Seed.Application.Common.Interfaces;
using Seed.Domain.Authorization;
using Seed.Domain.Enums;
using Seed.Infrastructure.Persistence;

namespace Seed.Infrastructure.Billing.Commands;

public sealed class CancelSubscriptionCommandHandler(
    ApplicationDbContext dbContext,
    IPaymentGateway paymentGateway,
    IAuditService auditService)
    : IRequestHandler<CancelSubscriptionCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(
        CancelSubscriptionCommand request, CancellationToken cancellationToken)
    {
        var subscription = await dbContext.UserSubscriptions
            .Where(s => s.UserId == request.UserId
                && (s.Status == SubscriptionStatus.Active || s.Status == SubscriptionStatus.Trialing))
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (subscription is null)
            return Result<bool>.Failure("No active subscription found.");

        if (string.IsNullOrWhiteSpace(subscription.StripeSubscriptionId))
            return Result<bool>.Failure("Subscription has no payment provider reference.");

        await paymentGateway.CancelSubscriptionAsync(subscription.StripeSubscriptionId, cancellationToken);

        subscription.CanceledAt = DateTime.UtcNow;
        subscription.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditService.LogAsync(
            AuditActions.SubscriptionCanceled,
            entityType: "UserSubscription",
            entityId: subscription.Id.ToString(),
            details: $"Subscription canceled for user {request.UserId}",
            userId: request.UserId,
            ipAddress: request.IpAddress,
            userAgent: request.UserAgent,
            cancellationToken: cancellationToken);

        return Result<bool>.Success(true);
    }
}
