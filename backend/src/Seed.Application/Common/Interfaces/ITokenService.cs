using Seed.Application.Common.Models;
using Seed.Domain.Entities;

namespace Seed.Application.Common.Interfaces;

public interface ITokenService
{
    Task<TokenResult> GenerateTokensAsync(ApplicationUser user, IList<string> roles);
    Task<TokenResult?> RefreshTokenAsync(string refreshToken);
    Task RevokeTokenAsync(string refreshToken);
    Task RevokeAllUserTokensAsync(Guid userId);
}
