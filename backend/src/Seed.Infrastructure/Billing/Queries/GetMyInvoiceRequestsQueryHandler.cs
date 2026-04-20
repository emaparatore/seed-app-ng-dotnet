using MediatR;
using Microsoft.EntityFrameworkCore;
using Seed.Application.Billing.Models;
using Seed.Application.Billing.Queries.GetMyInvoiceRequests;
using Seed.Application.Common;
using Seed.Infrastructure.Persistence;

namespace Seed.Infrastructure.Billing.Queries;

public sealed class GetMyInvoiceRequestsQueryHandler(ApplicationDbContext dbContext)
    : IRequestHandler<GetMyInvoiceRequestsQuery, Result<IReadOnlyList<InvoiceRequestDto>>>
{
    public async Task<Result<IReadOnlyList<InvoiceRequestDto>>> Handle(
        GetMyInvoiceRequestsQuery request, CancellationToken cancellationToken)
    {
        var items = await dbContext.InvoiceRequests
            .AsNoTracking()
            .Where(r => r.UserId == request.UserId)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new InvoiceRequestDto(
                r.Id,
                r.CustomerType.ToString(),
                r.FullName,
                r.CompanyName,
                r.Address,
                r.City,
                r.PostalCode,
                r.Country,
                r.FiscalCode,
                r.VatNumber,
                r.SdiCode,
                r.PecEmail,
                r.StripePaymentIntentId,
                r.Status.ToString(),
                r.CreatedAt,
                r.ProcessedAt))
            .ToListAsync(cancellationToken);

        return Result<IReadOnlyList<InvoiceRequestDto>>.Success(items);
    }
}
