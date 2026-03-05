using FluentAssertions;
using Seed.Domain.Entities;

namespace Seed.UnitTests.Domain;

public class RefreshTokenTests
{
    [Fact]
    public void IsExpired_Should_Be_True_When_ExpiresAt_Is_In_The_Past()
    {
        var token = new RefreshToken { ExpiresAt = DateTime.UtcNow.AddMinutes(-1) };
        token.IsExpired.Should().BeTrue();
    }

    [Fact]
    public void IsExpired_Should_Be_False_When_ExpiresAt_Is_In_The_Future()
    {
        var token = new RefreshToken { ExpiresAt = DateTime.UtcNow.AddHours(1) };
        token.IsExpired.Should().BeFalse();
    }

    [Fact]
    public void IsRevoked_Should_Be_True_When_RevokedAt_Is_Set()
    {
        var token = new RefreshToken { RevokedAt = DateTime.UtcNow };
        token.IsRevoked.Should().BeTrue();
    }

    [Fact]
    public void IsRevoked_Should_Be_False_When_RevokedAt_Is_Null()
    {
        var token = new RefreshToken { RevokedAt = null };
        token.IsRevoked.Should().BeFalse();
    }

    [Fact]
    public void IsActive_Should_Be_True_When_Not_Expired_And_Not_Revoked()
    {
        var token = new RefreshToken
        {
            ExpiresAt = DateTime.UtcNow.AddHours(1),
            RevokedAt = null
        };
        token.IsActive.Should().BeTrue();
    }

    [Fact]
    public void IsActive_Should_Be_False_When_Expired()
    {
        var token = new RefreshToken
        {
            ExpiresAt = DateTime.UtcNow.AddMinutes(-1),
            RevokedAt = null
        };
        token.IsActive.Should().BeFalse();
    }

    [Fact]
    public void IsActive_Should_Be_False_When_Revoked()
    {
        var token = new RefreshToken
        {
            ExpiresAt = DateTime.UtcNow.AddHours(1),
            RevokedAt = DateTime.UtcNow
        };
        token.IsActive.Should().BeFalse();
    }

    [Fact]
    public void IsActive_Should_Be_False_When_Both_Expired_And_Revoked()
    {
        var token = new RefreshToken
        {
            ExpiresAt = DateTime.UtcNow.AddMinutes(-1),
            RevokedAt = DateTime.UtcNow
        };
        token.IsActive.Should().BeFalse();
    }
}
