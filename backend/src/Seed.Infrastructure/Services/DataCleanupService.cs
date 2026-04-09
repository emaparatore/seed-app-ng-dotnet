using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Seed.Application.Common.Interfaces;
using Seed.Infrastructure.Persistence;
using Seed.Shared.Configuration;

namespace Seed.Infrastructure.Services;

public sealed class DataCleanupService(
    ApplicationDbContext dbContext,
    IUserPurgeService userPurgeService,
    IOptions<DataRetentionSettings> settings,
    ILogger<DataCleanupService> logger) : IDataCleanupService
{
    public async Task<int> PurgeSoftDeletedUsersAsync(CancellationToken cancellationToken = default)
    {
        var cutoffDate = DateTime.UtcNow.AddDays(-settings.Value.SoftDeletedUserRetentionDays);

        var usersToPurge = await dbContext.Users
            .IgnoreQueryFilters()
            .Where(u => u.IsDeleted && u.DeletedAt != null && u.DeletedAt < cutoffDate)
            .Select(u => u.Id)
            .ToListAsync(cancellationToken);

        foreach (var userId in usersToPurge)
        {
            await userPurgeService.PurgeUserAsync(userId, cancellationToken);
            logger.LogInformation("Purged soft-deleted user {UserId}", userId);
        }

        return usersToPurge.Count;
    }

    public async Task<int> CleanupExpiredRefreshTokensAsync(CancellationToken cancellationToken = default)
    {
        var cutoffDate = DateTime.UtcNow.AddDays(-settings.Value.RefreshTokenRetentionDays);

        var deleted = await dbContext.RefreshTokens
            .Where(r => r.ExpiresAt < DateTime.UtcNow || (r.RevokedAt != null && r.RevokedAt < cutoffDate))
            .ExecuteDeleteAsync(cancellationToken);

        return deleted;
    }

    public async Task<int> CleanupOldAuditLogEntriesAsync(CancellationToken cancellationToken = default)
    {
        var cutoffDate = DateTime.UtcNow.AddDays(-settings.Value.AuditLogRetentionDays);

        var deleted = await dbContext.AuditLogEntries
            .Where(a => a.Timestamp < cutoffDate)
            .ExecuteDeleteAsync(cancellationToken);

        return deleted;
    }
}
