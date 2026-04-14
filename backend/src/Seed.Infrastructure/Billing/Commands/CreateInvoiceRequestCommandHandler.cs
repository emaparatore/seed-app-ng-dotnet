using MediatR;
using Seed.Application.Billing.Commands.CreateInvoiceRequest;
using Seed.Application.Common;
using Seed.Application.Common.Interfaces;
using Seed.Domain.Authorization;
using Seed.Domain.Entities;
using Seed.Infrastructure.Persistence;

namespace Seed.Infrastructure.Billing.Commands;

public sealed class CreateInvoiceRequestCommandHandler(
    ApplicationDbContext dbContext,
    IAuditService auditService)
    : IRequestHandler<CreateInvoiceRequestCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(
        CreateInvoiceRequestCommand request, CancellationToken cancellationToken)
    {
        var invoiceRequest = new InvoiceRequest
        {
            Id = Guid.NewGuid(),
            UserId = request.UserId,
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
            StripePaymentIntentId = request.StripePaymentIntentId,
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
