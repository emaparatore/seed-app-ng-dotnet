using FluentAssertions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Seed.Infrastructure.Services;
using Seed.Shared.Configuration;

namespace Seed.IntegrationTests.Authorization;

public class TokenBlacklistServiceTests
{
    private readonly TokenBlacklistService _service;

    public TokenBlacklistServiceTests()
    {
        var cache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
        var jwtOptions = Options.Create(new JwtSettings
        {
            AccessTokenExpirationMinutes = 60,
            Secret = "test-secret-key-that-is-long-enough",
            Issuer = "test",
            Audience = "test",
            RefreshTokenExpirationDays = 7
        });
        _service = new TokenBlacklistService(cache, jwtOptions);
    }

    [Fact]
    public async Task IsUserTokenBlacklistedAsync_Should_Return_False_When_No_Blacklist_Entry()
    {
        var userId = Guid.NewGuid();

        var result = await _service.IsUserTokenBlacklistedAsync(userId, DateTime.UtcNow);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsUserTokenBlacklistedAsync_Should_Return_True_When_Token_Issued_Before_Blacklist()
    {
        var userId = Guid.NewGuid();
        var tokenIssuedAt = DateTime.UtcNow.AddMinutes(-5);

        await _service.BlacklistUserTokensAsync(userId);

        var result = await _service.IsUserTokenBlacklistedAsync(userId, tokenIssuedAt);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsUserTokenBlacklistedAsync_Should_Return_False_When_Token_Issued_After_Blacklist()
    {
        var userId = Guid.NewGuid();

        await _service.BlacklistUserTokensAsync(userId);

        // Token issued slightly in the future
        var tokenIssuedAt = DateTime.UtcNow.AddSeconds(1);

        var result = await _service.IsUserTokenBlacklistedAsync(userId, tokenIssuedAt);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task BlacklistUserTokensAsync_Should_Only_Affect_Specified_User()
    {
        var userId1 = Guid.NewGuid();
        var userId2 = Guid.NewGuid();
        var tokenIssuedAt = DateTime.UtcNow.AddMinutes(-5);

        await _service.BlacklistUserTokensAsync(userId1);

        var result1 = await _service.IsUserTokenBlacklistedAsync(userId1, tokenIssuedAt);
        var result2 = await _service.IsUserTokenBlacklistedAsync(userId2, tokenIssuedAt);

        result1.Should().BeTrue();
        result2.Should().BeFalse();
    }
}
