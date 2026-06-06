namespace Seed.Application.Admin.SystemHealth.Models;

public sealed record SystemHealthDto(
    ComponentStatusDto Database,
    ComponentStatusDto Email,
    PaymentsWebhookStatusDto PaymentsWebhook,
    string Version,
    string Environment,
    UptimeDto Uptime,
    MemoryDto Memory);

public sealed record ComponentStatusDto(string Status, string? Description);

public sealed record UptimeDto(long TotalSeconds, string Formatted);

public sealed record MemoryDto(double WorkingSetMegabytes, double GcAllocatedMegabytes);

public sealed record PaymentsWebhookStatusDto(
    string Status,
    string Description,
    DateTime? LastWebhookReceivedAt,
    DateTime? LastFailureAt,
    int RecentFailuresCount,
    int PendingCheckoutsCount,
    int StalePendingCheckoutsCount);
