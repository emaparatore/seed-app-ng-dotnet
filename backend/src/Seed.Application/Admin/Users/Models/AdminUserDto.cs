namespace Seed.Application.Admin.Users.Models;

public sealed record AdminUserDto(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    bool IsActive,
    IReadOnlyList<string> Roles,
    DateTime CreatedAt);
