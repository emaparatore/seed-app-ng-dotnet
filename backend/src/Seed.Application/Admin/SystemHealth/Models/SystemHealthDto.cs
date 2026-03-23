namespace Seed.Application.Admin.SystemHealth.Models;

public sealed record SystemHealthDto(
    ComponentStatusDto Database,
    ComponentStatusDto Email,
    string Version,
    string Environment,
    UptimeDto Uptime,
    MemoryDto Memory);

public sealed record ComponentStatusDto(string Status, string? Description);

public sealed record UptimeDto(long TotalSeconds, string Formatted);

public sealed record MemoryDto(double WorkingSetMegabytes, double GcAllocatedMegabytes);
