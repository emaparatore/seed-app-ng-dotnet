namespace Seed.Application.Admin.Roles.Models;

public sealed record PermissionDto(
    Guid Id,
    string Name,
    string? Description,
    string Category);
