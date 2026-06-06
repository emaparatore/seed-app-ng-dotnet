using MediatR;
using Microsoft.EntityFrameworkCore;
using Seed.Application.Billing.Commands.CreateInvoiceRequest;
using Seed.Application.Common;
using Seed.Application.Common.Interfaces;
using Seed.Domain.Authorization;
using Seed.Domain.Entities;
using Seed.Infrastructure.Persistence;

namespace Seed.Infrastructure.Billing.Commands;

public sealed class CreateInvoiceRequestCommandHandler(
    ApplicationDbContext dbContext,
    IPaymentGateway paymentGateway,
    IAuditService auditService)
    : IRequestHandler<CreateInvoiceRequestCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(
        CreateInvoiceRequestCommand request, CancellationToken cancellationToken)
    {
        var subscription = await dbContext.UserSubscriptions
            .AsNoTracking()
            .Include(s => s.Plan)
            .FirstOrDefaultAsync(
                s => s.Id == request.UserSubscriptionId && s.UserId == request.UserId,
                cancellationToken);

        if (subscription is null)
        {
            return Result<Guid>.Failure("Subscription reference not found for this user.");
        }

        var paymentDetails = string.IsNullOrWhiteSpace(subscription.StripeSubscriptionId)
            ? null
            : await paymentGateway.GetLatestPaidInvoiceAsync(subscription.StripeSubscriptionId, cancellationToken);

        var stripeInvoiceId = paymentDetails?.StripeInvoiceId;
        var stripePaymentIntentId = request.StripePaymentIntentId ?? paymentDetails?.StripePaymentIntentId;

        var alreadyExistsForPeriod = false;

        if (!string.IsNullOrWhiteSpace(stripeInvoiceId))
        {
            alreadyExistsForPeriod = await dbContext.InvoiceRequests
                .AsNoTracking()
                .AnyAsync(
                    r => r.UserId == request.UserId && r.StripeInvoiceId == stripeInvoiceId,
                    cancellationToken);
        }
        else if (!string.IsNullOrWhiteSpace(stripePaymentIntentId))
        {
            alreadyExistsForPeriod = await dbContext.InvoiceRequests
                .AsNoTracking()
                .AnyAsync(
                    r => r.UserId == request.UserId && r.StripePaymentIntentId == stripePaymentIntentId,
                    cancellationToken);
        }
        else
        {
            alreadyExistsForPeriod = await dbContext.InvoiceRequests
                .AsNoTracking()
                .AnyAsync(
                    r => r.UserId == request.UserId
                         && r.UserSubscriptionId == subscription.Id
                         && r.ServicePeriodStart == subscription.CurrentPeriodStart
                         && r.ServicePeriodEnd == subscription.CurrentPeriodEnd,
                    cancellationToken);
        }

        if (alreadyExistsForPeriod)
        {
            return Result<Guid>.Failure("An invoice request already exists for this billing transaction.");
        }

        var invoiceRequest = new InvoiceRequest
        {
            Id = Guid.NewGuid(),
            UserId = request.UserId,
            UserSubscriptionId = subscription.Id,
            CustomerType = request.CustomerType,
            FullName = request.FullName,
            CompanyName = request.CompanyName,
            Address = request.Address,
            City = request.City,
            PostalCode = request.PostalCode,
            Country = request.Country,
            FiscalCode = request.FiscalCode,
            VatNumber = request.VatNumber,
            SdiCode = request.SdiCode,
            PecEmail = request.PecEmail,
            StripeInvoiceId = stripeInvoiceId,
            StripePaymentIntentId = stripePaymentIntentId,
            ServiceName = subscription.Plan.Name,
            ServicePeriodStart = subscription.CurrentPeriodStart,
            ServicePeriodEnd = subscription.CurrentPeriodEnd,
            InvoicePeriodStart = paymentDetails?.InvoicePeriodStart,
            InvoicePeriodEnd = paymentDetails?.InvoicePeriodEnd,
            Currency = paymentDetails?.Currency,
            AmountSubtotal = paymentDetails?.AmountSubtotal,
            AmountTax = paymentDetails?.AmountTax,
            AmountTotal = paymentDetails?.AmountTotal,
            AmountPaid = paymentDetails?.AmountPaid,
            IsProrationApplied = paymentDetails?.IsProrationApplied,
            ProrationAmount = paymentDetails?.ProrationAmount,
            BillingReason = paymentDetails?.BillingReason,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        dbContext.InvoiceRequests.Add(invoiceRequest);
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditService.LogAsync(
            AuditActions.InvoiceRequestCreated,
            entityType: "InvoiceRequest",
            entityId: invoiceRequest.Id.ToString(),
            details: $"Invoice request created for user {request.UserId}",
            userId: request.UserId,
            ipAddress: request.IpAddress,
            userAgent: request.UserAgent,
            cancellationToken: cancellationToken);

        return Result<Guid>.Success(invoiceRequest.Id);
    }
}
