using MediatR;
using Microsoft.AspNetCore.Identity;
using Seed.Application.Common;
using Seed.Application.Common.Interfaces;
using Seed.Domain.Authorization;
using Seed.Domain.Entities;

namespace Seed.Application.Auth.Commands.DeleteAccount;

public sealed class DeleteAccountCommandHandler(
    UserManager<ApplicationUser> userManager,
    IUserPurgeService userPurgeService,
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

        // Write audit log before purge (while user data is still available)
        await auditService.LogAsync(AuditActions.AccountDeleted, "User", user.Id.ToString(), $"Email: {user.Email}", user.Id, cancellationToken: cancellationToken);

        // Hard delete: anonymize audit log, delete tokens, delete user
        await userPurgeService.PurgeUserAsync(user.Id, cancellationToken);

        return Result<bool>.Success(true);
    }
}
