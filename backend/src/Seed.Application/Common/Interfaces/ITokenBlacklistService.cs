namespace Seed.Application.Common.Interfaces;

public interface ITokenBlacklistService
{
    Task BlacklistUserTokensAsync(Guid userId);
    Task<bool> IsUserTokenBlacklistedAsync(Guid userId, DateTime tokenIssuedAt);
}
