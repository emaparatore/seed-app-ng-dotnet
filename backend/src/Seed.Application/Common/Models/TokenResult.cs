namespace Seed.Application.Common.Models;

public sealed record TokenResult(string AccessToken, string RefreshToken, DateTime ExpiresAt, Guid UserId);
