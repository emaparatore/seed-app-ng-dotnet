using MediatR;
using Microsoft.EntityFrameworkCore;
using Seed.Application.Admin.InvoiceRequests.Models;
using Seed.Application.Admin.InvoiceRequests.Queries.GetInvoiceRequests;
using Seed.Application.Common;
using Seed.Application.Common.Models;
using Seed.Domain.Enums;
using Seed.Infrastructure.Persistence;

namespace Seed.Infrastructure.Billing.Queries;

public sealed class GetAdminInvoiceRequestsQueryHandler(ApplicationDbContext dbContext)
    : IRequestHandler<GetInvoiceRequestsQuery, Result<PagedResult<AdminInvoiceRequestDto>>>
{
    public async Task<Result<PagedResult<AdminInvoiceRequestDto>>> Handle(
        GetInvoiceRequestsQuery request, CancellationToken cancellationToken)
    {
        var query = dbContext.InvoiceRequests
            .AsNoTracking()
            .Include(r => r.User)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.StatusFilter) &&
            Enum.TryParse<InvoiceRequestStatus>(request.StatusFilter, ignoreCase: true, out var status))
        {
            query = query.Where(r => r.Status == status);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(r => r.CreatedAt)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(r => new AdminInvoiceRequestDto(
                r.Id,
                r.User.Email!,
                r.User.FirstName + " " + r.User.LastName,
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

        return Result<PagedResult<AdminInvoiceRequestDto>>.Success(
            new PagedResult<AdminInvoiceRequestDto>(items, request.PageNumber, request.PageSize, totalCount));
    }
}
