using MediatR;
using Seed.Application.Admin.InvoiceRequests.Models;
using Seed.Application.Common;
using Seed.Application.Common.Models;

namespace Seed.Application.Admin.InvoiceRequests.Queries.GetInvoiceRequests;

public sealed record GetInvoiceRequestsQuery : IRequest<Result<PagedResult<AdminInvoiceRequestDto>>>
{
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 10;
    public string? StatusFilter { get; init; }
}
