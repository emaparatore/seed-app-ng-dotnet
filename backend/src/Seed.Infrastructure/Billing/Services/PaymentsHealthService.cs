using Microsoft.EntityFrameworkCore;
using Seed.Application.Admin.SystemHealth.Models;
using Seed.Application.Common.Interfaces;
using Seed.Domain.Authorization;
using Seed.Domain.Enums;
using Seed.Infrastructure.Persistence;

namespace Seed.Infrastructure.Billing.Services;

public sealed class PaymentsHealthService(ApplicationDbContext dbContext) : IPaymentsHealthService
{
    public async Task<PaymentsWebhookStatusDto> GetWebhookStatusAsync(CancellationToken ct = default)
    {
        var staleThreshold = DateTime.UtcNow.AddMinutes(-10);
        var pendingCheckoutsCount = await dbContext.CheckoutSessionAttempts
            .CountAsync(a => a.Status == CheckoutSessionAttemptStatus.Pending, ct);
        var stalePendingCheckoutsCount = await dbContext.CheckoutSessionAttempts
            .CountAsync(a => a.Status == CheckoutSessionAttemptStatus.Pending && a.CreatedAt <= staleThreshold, ct);

        var lastWebhookReceivedAt = await dbContext.AuditLogEntries
            .Where(a => a.Action == AuditActions.WebhookReceived)
            .OrderByDescending(a => a.Timestamp)
            .Select(a => (DateTime?)a.Timestamp)
            .FirstOrDefaultAsync(ct);

        var recentFailureWindow = DateTime.UtcNow.AddHours(-1);
        var lastFailureAt = await dbContext.AuditLogEntries
            .Where(a => a.Action == AuditActions.WebhookVerificationFailed
                || a.Action == AuditActions.WebhookProcessingFailed
                || a.Action == AuditActions.CheckoutSessionConfirmationFailed)
            .OrderByDescending(a => a.Timestamp)
            .Select(a => (DateTime?)a.Timestamp)
            .FirstOrDefaultAsync(ct);
        var recentFailuresCount = await dbContext.AuditLogEntries
            .CountAsync(a => (a.Action == AuditActions.WebhookVerificationFailed
                    || a.Action == AuditActions.WebhookProcessingFailed
                    || a.Action == AuditActions.CheckoutSessionConfirmationFailed)
                && a.Timestamp >= recentFailureWindow,
                ct);

        var status = stalePendingCheckoutsCount >= 3 || recentFailuresCount >= 3
            ? "Unhealthy"
            : stalePendingCheckoutsCount > 0 || recentFailuresCount > 0
                ? "Degraded"
                : "Healthy";
        var description = $"Pending checkouts: {pendingCheckoutsCount}, stale: {stalePendingCheckoutsCount}, failures (last hour): {recentFailuresCount}";

        return new PaymentsWebhookStatusDto(
            Status: status,
            Description: description,
            LastWebhookReceivedAt: lastWebhookReceivedAt,
            LastFailureAt: lastFailureAt,
            RecentFailuresCount: recentFailuresCount,
            PendingCheckoutsCount: pendingCheckoutsCount,
            StalePendingCheckoutsCount: stalePendingCheckoutsCount);
    }
}
