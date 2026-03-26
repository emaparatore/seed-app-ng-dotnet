using MediatR;
using Seed.Application.Admin.AuditLog.Models;
using Seed.Application.Common;
using Seed.Application.Common.Interfaces;

namespace Seed.Application.Admin.AuditLog.Queries.GetAuditLogEntryById;

public sealed class GetAuditLogEntryByIdQueryHandler(
    IAuditLogReader auditLogReader)
    : IRequestHandler<GetAuditLogEntryByIdQuery, Result<AuditLogEntryDto>>
{
    public async Task<Result<AuditLogEntryDto>> Handle(
        GetAuditLogEntryByIdQuery request, CancellationToken cancellationToken)
    {
        var entry = await auditLogReader.GetByIdAsync(request.Id, cancellationToken);

        if (entry is null)
            return Result<AuditLogEntryDto>.Failure("Audit log entry not found.");

        var dto = new AuditLogEntryDto(
            entry.Id, entry.Timestamp, entry.UserId, entry.Action, entry.EntityType,
            entry.EntityId, entry.Details, entry.IpAddress, entry.UserAgent);

        return Result<AuditLogEntryDto>.Success(dto);
    }
}
