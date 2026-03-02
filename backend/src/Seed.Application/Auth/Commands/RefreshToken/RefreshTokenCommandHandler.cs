using MediatR;
using Microsoft.AspNetCore.Identity;
using Seed.Application.Common;
using Seed.Application.Common.Interfaces;
using Seed.Application.Common.Models;
using Seed.Domain.Entities;

namespace Seed.Application.Auth.Commands.RefreshToken;

public sealed class RefreshTokenCommandHandler(
    ITokenService tokenService,
    UserManager<ApplicationUser> userManager) : IRequestHandler<RefreshTokenCommand, Result<AuthResponse>>
{
    public async Task<Result<AuthResponse>> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        var tokens = await tokenService.RefreshTokenAsync(request.RefreshToken);
        if (tokens is null)
            return Result<AuthResponse>.Failure("Invalid or expired refresh token.");

        var user = await userManager.FindByIdAsync(tokens.UserId.ToString());
        if (user is null || !user.IsActive)
            return Result<AuthResponse>.Failure("Invalid or expired refresh token.");

        return Result<AuthResponse>.Success(new AuthResponse(
            tokens.AccessToken,
            tokens.RefreshToken,
            tokens.ExpiresAt,
            new UserDto(user.Id, user.Email!, user.FirstName, user.LastName)));
    }
}
