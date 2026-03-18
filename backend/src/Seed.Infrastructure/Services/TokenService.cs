using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Seed.Application.Common.Interfaces;
using Seed.Application.Common.Models;
using Seed.Domain.Entities;
using Seed.Infrastructure.Persistence;
using Seed.Shared.Configuration;

namespace Seed.Infrastructure.Services;

public sealed class TokenService(
    IOptions<JwtSettings> jwtOptions,
    ApplicationDbContext dbContext,
    UserManager<ApplicationUser> userManager) : ITokenService
{
    private readonly JwtSettings _jwt = jwtOptions.Value;

    public async Task<TokenResult> GenerateTokensAsync(ApplicationUser user, IList<string> roles)
    {
        var expiresAt = DateTime.UtcNow.AddMinutes(_jwt.AccessTokenExpirationMinutes);
        var accessToken = GenerateAccessToken(user, roles, expiresAt);
        var refreshToken = await CreateRefreshTokenAsync(user.Id);

        return new TokenResult(accessToken, refreshToken, expiresAt, user.Id);
    }

    public async Task<TokenResult?> RefreshTokenAsync(string refreshToken)
    {
        var storedToken = await dbContext.RefreshTokens
            .Include(r => r.User)
            .FirstOrDefaultAsync(r => r.Token == refreshToken);

        if (storedToken is null || !storedToken.IsActive)
            return null;

        // Revoke the old token (rotation)
        storedToken.RevokedAt = DateTime.UtcNow;

        var user = storedToken.User;
        if (!user.IsActive)
            return null;

        var roles = await userManager.GetRolesAsync(user);

        var expiresAt = DateTime.UtcNow.AddMinutes(_jwt.AccessTokenExpirationMinutes);
        var newAccessToken = GenerateAccessToken(user, roles, expiresAt);
        var newRefreshToken = await CreateRefreshTokenAsync(user.Id);

        storedToken.ReplacedByToken = newRefreshToken;
        await dbContext.SaveChangesAsync();

        return new TokenResult(newAccessToken, newRefreshToken, expiresAt, user.Id);
    }

    public async Task RevokeTokenAsync(string refreshToken)
    {
        var storedToken = await dbContext.RefreshTokens
            .FirstOrDefaultAsync(r => r.Token == refreshToken);

        if (storedToken is not null && storedToken.IsActive)
        {
            storedToken.RevokedAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync();
        }
    }

    public async Task RevokeAllUserTokensAsync(Guid userId)
    {
        var activeTokens = await dbContext.RefreshTokens
            .Where(r => r.UserId == userId && r.RevokedAt == null)
            .ToListAsync();

        foreach (var token in activeTokens)
        {
            token.RevokedAt = DateTime.UtcNow;
        }

        await dbContext.SaveChangesAsync();
    }

    private string GenerateAccessToken(ApplicationUser user, IList<string> roles, DateTime expiresAt)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email!),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),
            new("firstName", user.FirstName),
            new("lastName", user.LastName)
        };

        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.Secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _jwt.Issuer,
            audience: _jwt.Audience,
            claims: claims,
            expires: expiresAt,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private async Task<string> CreateRefreshTokenAsync(Guid userId)
    {
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

        var refreshToken = new RefreshToken
        {
            Token = token,
            UserId = userId,
            ExpiresAt = DateTime.UtcNow.AddDays(_jwt.RefreshTokenExpirationDays)
        };

        dbContext.RefreshTokens.Add(refreshToken);
        await dbContext.SaveChangesAsync();

        return token;
    }
}
