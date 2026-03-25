using MediatR;
using Microsoft.AspNetCore.Identity;
using Seed.Application.Common;
using Seed.Application.Common.Interfaces;
using Seed.Domain.Authorization;
using Seed.Domain.Entities;

namespace Seed.Application.Auth.Commands.DeleteAccount;

public sealed class DeleteAccountCommandHandler(
    UserManager<ApplicationUser> userManager,
    ITokenService tokenService,
    IAuditService auditService) : IRequestHandler<DeleteAccountCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(DeleteAccountCommand request, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(request.UserId.ToString());
        if (user is null || !user.IsActive)
            return Result<bool>.Failure("Account not found.");

        var validPassword = await userManager.CheckPasswordAsync(user, request.Password);
        if (!validPassword)
            return Result<bool>.Failure("Invalid password.");

        user.IsActive = false;
        user.UpdatedAt = DateTime.UtcNow;
        await userManager.UpdateAsync(user);

        await tokenService.RevokeAllUserTokensAsync(user.Id);
        await auditService.LogAsync(AuditActions.AccountDeleted, "User", user.Id.ToString(), $"Email: {user.Email}", user.Id, cancellationToken: cancellationToken);

        return Result<bool>.Success(true);
    }
}
