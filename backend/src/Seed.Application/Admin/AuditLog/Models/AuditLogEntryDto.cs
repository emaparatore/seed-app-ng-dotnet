namespace Seed.Application.Admin.AuditLog.Models;

public sealed record AuditLogEntryDto(
    Guid Id,
    DateTime Timestamp,
    Guid? UserId,
    string Action,
    string EntityType,
    string? EntityId,
    string? Details,
    string? IpAddress,
    string? UserAgent);
