using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Seed.Application.Common.Interfaces;
using Seed.Domain.Authorization;
using Seed.Domain.Entities;

namespace Seed.Infrastructure.Billing.Services;

public sealed class PaymentsHealthAlertBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger<PaymentsHealthAlertBackgroundService> logger) : BackgroundService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan AlertCooldown = TimeSpan.FromHours(1);

    private string _lastStatus = "Healthy";
    private DateTime _lastAlertAt = DateTime.MinValue;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Payments health alert background service started");

        using var timer = new PeriodicTimer(CheckInterval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await RunAlertCheckAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Payments health alert cycle failed");
            }
        }
    }

    internal async Task RunAlertCheckAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var healthService = scope.ServiceProvider.GetRequiredService<IPaymentsHealthService>();
        var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();
        var auditService = scope.ServiceProvider.GetRequiredService<IAuditService>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var health = await healthService.GetWebhookStatusAsync(cancellationToken);
        if (string.Equals(health.Status, "Healthy", StringComparison.OrdinalIgnoreCase))
        {
            _lastStatus = health.Status;
            return;
        }

        var now = DateTime.UtcNow;
        var statusChanged = !string.Equals(_lastStatus, health.Status, StringComparison.OrdinalIgnoreCase);
        var cooldownElapsed = now - _lastAlertAt >= AlertCooldown;

        if (!statusChanged && !cooldownElapsed)
            return;

        _lastStatus = health.Status;
        _lastAlertAt = now;

        var superAdmins = await userManager.GetUsersInRoleAsync(SystemRoles.SuperAdmin);
        if (superAdmins.Count == 0)
        {
            logger.LogWarning("Payments health degraded but no SuperAdmin users found for alerting");
            return;
        }

        var subject = $"[Seed] Payments health {health.Status}";
        var message =
            $"Payments/webhook health is {health.Status}. {health.Description}. Last webhook: {(health.LastWebhookReceivedAt?.ToString("u") ?? "n/a")}. Last failure: {(health.LastFailureAt?.ToString("u") ?? "n/a")}.";

        foreach (var admin in superAdmins.Where(u => !string.IsNullOrWhiteSpace(u.Email)))
        {
            await emailService.SendOperationalAlertAsync(admin.Email!, subject, message, cancellationToken);
        }

        await auditService.LogAsync(
            AuditActions.PaymentsHealthAlertTriggered,
            entityType: "SystemHealth",
            details: message,
            cancellationToken: cancellationToken);

        logger.LogWarning("Payments health alert sent: {Message}", message);
    }
}
