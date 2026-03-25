namespace Seed.Application.Common.Models;

public sealed record AuthResponse(
    string AccessToken,
    string RefreshToken,
    DateTime ExpiresAt,
    UserDto User,
    IReadOnlyList<string> Permissions,
    bool MustChangePassword);
