namespace Seed.Application.Admin.Dashboard.Models;

public sealed record RecentActivityDto(
    Guid Id,
    DateTime Timestamp,
    string Action,
    string EntityType,
    Guid? UserId);
