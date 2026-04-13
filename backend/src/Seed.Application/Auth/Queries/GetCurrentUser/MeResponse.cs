namespace Seed.Application.Auth.Queries.GetCurrentUser;

public sealed record MeResponse(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions,
    SubscriptionInfoDto? Subscription);
