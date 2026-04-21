using MediatR;
using Microsoft.EntityFrameworkCore;
using Seed.Application.Admin.InvoiceRequests.Commands.UpdateInvoiceRequestStatus;
using Seed.Application.Common;
using Seed.Application.Common.Interfaces;
using Seed.Domain.Authorization;
using Seed.Domain.Enums;
using Seed.Infrastructure.Persistence;

namespace Seed.Infrastructure.Billing.Commands;

public sealed class UpdateInvoiceRequestStatusCommandHandler(
    ApplicationDbContext dbContext,
    IAuditService auditService)
    : IRequestHandler<UpdateInvoiceRequestStatusCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(
        UpdateInvoiceRequestStatusCommand request, CancellationToken cancellationToken)
    {
        var invoiceRequest = await dbContext.InvoiceRequests
            .FirstOrDefaultAsync(r => r.Id == request.InvoiceRequestId, cancellationToken);

        if (invoiceRequest is null)
            return Result<bool>.Failure("Invoice request not found.");

        invoiceRequest.Status = request.NewStatus;
        invoiceRequest.UpdatedAt = DateTime.UtcNow;

        if (request.NewStatus == InvoiceRequestStatus.Issued)
            invoiceRequest.ProcessedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);

        await auditService.LogAsync(
            AuditActions.InvoiceRequestStatusUpdated,
            entityType: "InvoiceRequest",
            entityId: invoiceRequest.Id.ToString(),
            details: $"Invoice request status updated to {request.NewStatus} by user {request.CurrentUserId}",
            userId: request.CurrentUserId,
            ipAddress: request.IpAddress,
            userAgent: request.UserAgent,
            cancellationToken: cancellationToken);

        return Result<bool>.Success(true);
    }
}
