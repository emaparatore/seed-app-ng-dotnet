using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Seed.Application.Billing.Commands.CreateCheckoutSession;
using Seed.Application.Billing.Models;
using Seed.Application.Common;
using Seed.Application.Common.Interfaces;
using Seed.Application.Common.Models;
using Seed.Domain.Authorization;
using Seed.Domain.Entities;
using Seed.Domain.Enums;
using Seed.Infrastructure.Persistence;

namespace Seed.Infrastructure.Billing.Commands;

public sealed class CreateCheckoutSessionCommandHandler(
    ApplicationDbContext dbContext,
    UserManager<ApplicationUser> userManager,
    IPaymentGateway paymentGateway,
    IAuditService auditService)
    : IRequestHandler<CreateCheckoutSessionCommand, Result<CheckoutSessionResponse>>
{
    public async Task<Result<CheckoutSessionResponse>> Handle(
        CreateCheckoutSessionCommand request, CancellationToken cancellationToken)
    {
        var plan = await dbContext.SubscriptionPlans
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == request.PlanId, cancellationToken);

        if (plan is null)
            return Result<CheckoutSessionResponse>.Failure("Plan not found.");

        if (plan.Status != PlanStatus.Active)
            return Result<CheckoutSessionResponse>.Failure("Plan is not active.");

        if (plan.IsFreeTier)
            return Result<CheckoutSessionResponse>.Failure("Cannot create a checkout session for a free tier plan.");

        var priceId = request.BillingInterval == BillingInterval.Monthly
            ? plan.StripePriceIdMonthly
            : plan.StripePriceIdYearly;

        if (string.IsNullOrWhiteSpace(priceId))
            return Result<CheckoutSessionResponse>.Failure($"No price configured for {request.BillingInterval} billing interval.");

        var user = await userManager.FindByIdAsync(request.UserId.ToString());
        if (user is null)
            return Result<CheckoutSessionResponse>.Failure("User not found.");

        var existingCustomerId = await dbContext.UserSubscriptions
            .AsNoTracking()
            .Where(s => s.UserId == request.UserId && s.StripeCustomerId != null)
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => s.StripeCustomerId)
            .FirstOrDefaultAsync(cancellationToken);

        var customerId = existingCustomerId;
        if (string.IsNullOrWhiteSpace(customerId))
        {
            customerId = await paymentGateway.CreateCustomerAsync(
                user.Email!, $"{user.FirstName} {user.LastName}".Trim(), cancellationToken);
        }

        var metadata = new Dictionary<string, string>
        {
            ["userId"] = request.UserId.ToString(),
            ["planId"] = request.PlanId.ToString()
        };

        var checkoutRequest = new CreateCheckoutRequest(
            PriceId: priceId,
            CustomerEmail: user.Email!,
            CustomerId: customerId,
            SuccessUrl: request.SuccessUrl,
            CancelUrl: request.CancelUrl,
            TrialDays: plan.TrialDays > 0 ? plan.TrialDays : null,
            Metadata: metadata);

        var checkoutUrl = await paymentGateway.CreateCheckoutSessionAsync(checkoutRequest, cancellationToken);

        await auditService.LogAsync(
            AuditActions.CheckoutSessionCreated,
            entityType: "SubscriptionPlan",
            entityId: request.PlanId.ToString(),
            details: $"Checkout session created for plan '{plan.Name}' ({request.BillingInterval})",
            userId: request.UserId,
            ipAddress: request.IpAddress,
            userAgent: request.UserAgent,
            cancellationToken: cancellationToken);

        return Result<CheckoutSessionResponse>.Success(new CheckoutSessionResponse(checkoutUrl));
    }
}
