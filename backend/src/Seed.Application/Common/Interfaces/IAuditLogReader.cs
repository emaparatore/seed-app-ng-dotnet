using Seed.Domain.Entities;

namespace Seed.Application.Common.Interfaces;

public interface IAuditLogReader
{
    IQueryable<AuditLogEntry> GetQueryable();
    Task<AuditLogEntry?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
}
