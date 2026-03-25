using MediatR;
using Microsoft.AspNetCore.Identity;
using Seed.Application.Common;
using Seed.Application.Common.Interfaces;
using Seed.Application.Common.Models;
using Seed.Domain.Authorization;
using Seed.Domain.Entities;

namespace Seed.Application.Auth.Commands.ConfirmEmail;

public sealed class ConfirmEmailCommandHandler(
    UserManager<ApplicationUser> userManager,
    ITokenService tokenService,
    IPermissionService permissionService,
    IAuditService auditService) : IRequestHandler<ConfirmEmailCommand, Result<AuthResponse>>
{
    public async Task<Result<AuthResponse>> Handle(ConfirmEmailCommand request, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null || !user.IsActive)
            return Result<AuthResponse>.Failure("Invalid or expired verification link.");

        if (user.EmailConfirmed)
            return Result<AuthResponse>.Failure("Email address has already been verified.");

        var result = await userManager.ConfirmEmailAsync(user, request.Token);
        if (!result.Succeeded)
            return Result<AuthResponse>.Failure("Invalid or expired verification link.");

        await auditService.LogAsync(AuditActions.EmailConfirmed, "User", user.Id.ToString(), $"Email: {request.Email}", user.Id, cancellationToken: cancellationToken);

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
