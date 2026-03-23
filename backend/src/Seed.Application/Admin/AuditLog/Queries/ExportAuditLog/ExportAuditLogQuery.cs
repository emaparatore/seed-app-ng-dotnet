using MediatR;
using Seed.Application.Common;

namespace Seed.Application.Admin.AuditLog.Queries.ExportAuditLog;

public sealed record ExportAuditLogQuery : IRequest<Result<byte[]>>
{
    public string? ActionFilter { get; init; }
    public Guid? UserId { get; init; }
    public DateTime? DateFrom { get; init; }
    public DateTime? DateTo { get; init; }
    public string? SearchTerm { get; init; }
    public bool SortDescending { get; init; } = true;
}
