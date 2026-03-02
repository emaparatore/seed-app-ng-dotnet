using Seed.Application.Common.Models;
using Seed.Domain.Entities;

namespace Seed.Application.Common.Interfaces;

public interface ITokenService
{
    Task<TokenResult> GenerateTokensAsync(ApplicationUser user);
    Task<TokenResult?> RefreshTokenAsync(string refreshToken);
    Task RevokeTokenAsync(string refreshToken);
}
