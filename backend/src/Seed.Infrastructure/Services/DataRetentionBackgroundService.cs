using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Seed.Application.Common.Interfaces;
using Seed.Shared.Configuration;

namespace Seed.Infrastructure.Services;

public sealed class DataRetentionBackgroundService(
    IServiceScopeFactory scopeFactory,
    IOptions<DataRetentionSettings> settings,
    ILogger<DataRetentionBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalHours = settings.Value.CleanupIntervalHours;
        logger.LogInformation("Data retention background service started with interval of {IntervalHours} hours", intervalHours);

        using var timer = new PeriodicTimer(TimeSpan.FromHours(intervalHours));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RunCleanupCycleAsync(stoppingToken);
        }
    }

    internal async Task RunCleanupCycleAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Starting data retention cleanup cycle");

        using var scope = scopeFactory.CreateScope();
        var cleanupService = scope.ServiceProvider.GetRequiredService<IDataCleanupService>();

        var purgedUsers = 0;
        var cleanedTokens = 0;
        var removedAuditLogs = 0;

        try
        {
            purgedUsers = await cleanupService.PurgeSoftDeletedUsersAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Error purging soft-deleted users");
        }

        try
        {
            cleanedTokens = await cleanupService.CleanupExpiredRefreshTokensAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Error cleaning up expired refresh tokens");
        }

        try
        {
            removedAuditLogs = await cleanupService.CleanupOldAuditLogEntriesAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Error cleaning up old audit log entries");
        }

        logger.LogInformation(
            "Data retention cleanup completed: purged {PurgedUsers} soft-deleted users, cleaned {CleanedTokens} expired tokens, removed {RemovedAuditLogs} audit log entries",
            purgedUsers, cleanedTokens, removedAuditLogs);
    }
}
