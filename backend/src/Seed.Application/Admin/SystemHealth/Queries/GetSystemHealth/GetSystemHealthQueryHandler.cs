using System.Diagnostics;
using System.Reflection;
using MediatR;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Seed.Application.Admin.SystemHealth.Models;
using Seed.Application.Common;
using Seed.Application.Common.Interfaces;
using Seed.Shared.Configuration;

namespace Seed.Application.Admin.SystemHealth.Queries.GetSystemHealth;

public sealed class GetSystemHealthQueryHandler(
    HealthCheckService healthCheckService,
    IOptions<SmtpSettings> smtpSettings,
    IHostEnvironment hostEnvironment,
    IPaymentsHealthService paymentsHealthService)
    : IRequestHandler<GetSystemHealthQuery, Result<SystemHealthDto>>
{
    public async Task<Result<SystemHealthDto>> Handle(
        GetSystemHealthQuery request, CancellationToken cancellationToken)
    {
        // Database status via health check
        var healthReport = await healthCheckService.CheckHealthAsync(
            registration => registration.Name == "postgresql", cancellationToken);
        var dbEntry = healthReport.Entries.GetValueOrDefault("postgresql");
        var dbStatus = new ComponentStatusDto(
            healthReport.Status.ToString(),
            dbEntry.Description ?? healthReport.Status.ToString());

        // Email status
        var smtp = smtpSettings.Value;
        var emailConfigured = !string.IsNullOrWhiteSpace(smtp.Host);
        var emailStatus = new ComponentStatusDto(
            emailConfigured ? "Configured" : "NotConfigured",
            emailConfigured ? $"SMTP: {smtp.Host}:{smtp.Port}" : "Using console fallback");

        var paymentsWebhook = await paymentsHealthService.GetWebhookStatusAsync(cancellationToken);

        // Version
        var version = Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "unknown";

        // Environment
        var environment = hostEnvironment.EnvironmentName;

        // Uptime
        var process = Process.GetCurrentProcess();
        var uptime = DateTime.UtcNow - process.StartTime.ToUniversalTime();
        var formatted = uptime.Days > 0
            ? $"{uptime.Days}d {uptime.Hours}h {uptime.Minutes}m"
            : $"{uptime.Hours}h {uptime.Minutes}m {uptime.Seconds}s";
        var uptimeDto = new UptimeDto((long)uptime.TotalSeconds, formatted);

        // Memory
        var workingSetMb = process.WorkingSet64 / (1024.0 * 1024.0);
        var gcAllocatedMb = GC.GetTotalMemory(false) / (1024.0 * 1024.0);
        var memoryDto = new MemoryDto(
            Math.Round(workingSetMb, 2),
            Math.Round(gcAllocatedMb, 2));

        var dto = new SystemHealthDto(dbStatus, emailStatus, paymentsWebhook, version, environment, uptimeDto, memoryDto);
        return Result<SystemHealthDto>.Success(dto);
    }
}
