namespace Seed.Application.Admin.Roles.Models;

public sealed record AdminRoleDto(
    Guid Id,
    string Name,
    string? Description,
    bool IsSystemRole,
    int UserCount,
    DateTime CreatedAt);
