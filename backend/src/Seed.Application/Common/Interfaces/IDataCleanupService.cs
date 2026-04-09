namespace Seed.Application.Common.Interfaces;

public interface IDataCleanupService
{
    Task<int> PurgeSoftDeletedUsersAsync(CancellationToken cancellationToken = default);
    Task<int> CleanupExpiredRefreshTokensAsync(CancellationToken cancellationToken = default);
    Task<int> CleanupOldAuditLogEntriesAsync(CancellationToken cancellationToken = default);
}
