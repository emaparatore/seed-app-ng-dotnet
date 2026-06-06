namespace Seed.Application.Admin.Users.Models;

public sealed record AdminUserDetailDto(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    bool IsActive,
    IReadOnlyList<string> Roles,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    bool MustChangePassword,
    bool EmailConfirmed,
    AdminUserSubscriptionDto? Subscription);
