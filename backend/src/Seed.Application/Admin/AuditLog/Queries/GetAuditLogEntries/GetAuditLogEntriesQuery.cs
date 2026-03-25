using MediatR;
using Seed.Application.Admin.AuditLog.Models;
using Seed.Application.Common;
using Seed.Application.Common.Models;

namespace Seed.Application.Admin.AuditLog.Queries.GetAuditLogEntries;

public sealed record GetAuditLogEntriesQuery : IRequest<Result<PagedResult<AuditLogEntryDto>>>
{
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 10;
    public string? ActionFilter { get; init; }
    public Guid? UserId { get; init; }
    public DateTime? DateFrom { get; init; }
    public DateTime? DateTo { get; init; }
    public string? SearchTerm { get; init; }
    public bool SortDescending { get; init; } = true;
}
