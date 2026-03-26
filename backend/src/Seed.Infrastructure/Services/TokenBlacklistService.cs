using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using Seed.Application.Common.Interfaces;
using Seed.Shared.Configuration;

namespace Seed.Infrastructure.Services;

public sealed class TokenBlacklistService(
    IDistributedCache cache,
    IOptions<JwtSettings> jwtOptions) : ITokenBlacklistService
{
    private const string CacheKeyPrefix = "token_blacklist:user:";
    private readonly JwtSettings _jwt = jwtOptions.Value;

    public async Task BlacklistUserTokensAsync(Guid userId)
    {
        var cacheKey = $"{CacheKeyPrefix}{userId}";
        var blacklistTime = DateTime.UtcNow.Ticks.ToString();

        await cache.SetStringAsync(cacheKey, blacklistTime, new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_jwt.AccessTokenExpirationMinutes)
        });
    }

    public async Task<bool> IsUserTokenBlacklistedAsync(Guid userId, DateTime tokenIssuedAt)
    {
        var cacheKey = $"{CacheKeyPrefix}{userId}";
        var value = await cache.GetStringAsync(cacheKey);

        if (value is null)
            return false;

        var blacklistTime = new DateTime(long.Parse(value), DateTimeKind.Utc);
        return tokenIssuedAt < blacklistTime;
    }
}
