using MediatR;
using Microsoft.AspNetCore.Identity;
using Seed.Application.Common;
using Seed.Application.Common.Interfaces;
using Seed.Application.Common.Models;
using Seed.Domain.Entities;

namespace Seed.Application.Auth.Commands.Login;

public sealed class LoginCommandHandler(
    UserManager<ApplicationUser> userManager,
    ITokenService tokenService,
    IPermissionService permissionService) : IRequestHandler<LoginCommand, Result<AuthResponse>>
{
    public async Task<Result<AuthResponse>> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null)
            return Result<AuthResponse>.Failure("Invalid email or password.");

        if (!user.IsActive)
            return Result<AuthResponse>.Failure("This account has been deactivated.");

        var validPassword = await userManager.CheckPasswordAsync(user, request.Password);
        if (!validPassword)
            return Result<AuthResponse>.Failure("Invalid email or password.");

        if (!user.EmailConfirmed)
            return Result<AuthResponse>.Failure("Please verify your email address before logging in. Check your inbox for the verification link.");

        var roles = await userManager.GetRolesAsync(user);
        var tokens = await tokenService.GenerateTokensAsync(user, roles);
        var permissions = await permissionService.GetPermissionsAsync(user.Id);

        return Result<AuthResponse>.Success(new AuthResponse(
            tokens.AccessToken,
            tokens.RefreshToken,
            tokens.ExpiresAt,
            new UserDto(user.Id, user.Email!, user.FirstName, user.LastName, roles.ToList().AsReadOnly()),
            permissions.ToList().AsReadOnly(),
            user.MustChangePassword));
    }
}
