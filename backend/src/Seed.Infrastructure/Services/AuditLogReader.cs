using Microsoft.EntityFrameworkCore;
using Seed.Application.Common.Interfaces;
using Seed.Domain.Entities;
using Seed.Infrastructure.Persistence;

namespace Seed.Infrastructure.Services;

public class AuditLogReader(ApplicationDbContext dbContext) : IAuditLogReader
{
    public IQueryable<AuditLogEntry> GetQueryable()
    {
        return dbContext.AuditLogEntries.AsNoTracking();
    }

    public async Task<AuditLogEntry?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return await dbContext.AuditLogEntries
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
    }
}
