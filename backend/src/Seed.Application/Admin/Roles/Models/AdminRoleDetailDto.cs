namespace Seed.Application.Admin.Roles.Models;

public sealed record AdminRoleDetailDto(
    Guid Id,
    string Name,
    string? Description,
    bool IsSystemRole,
    int UserCount,
    DateTime CreatedAt,
    IReadOnlyList<string> Permissions);
