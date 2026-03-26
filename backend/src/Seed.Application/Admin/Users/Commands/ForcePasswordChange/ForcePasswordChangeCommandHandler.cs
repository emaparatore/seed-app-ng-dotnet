using MediatR;
using Microsoft.AspNetCore.Identity;
using Seed.Application.Common;
using Seed.Application.Common.Interfaces;
using Seed.Domain.Authorization;
using Seed.Domain.Entities;

namespace Seed.Application.Admin.Users.Commands.ForcePasswordChange;

public sealed class ForcePasswordChangeCommandHandler(
    UserManager<ApplicationUser> userManager,
    ITokenBlacklistService tokenBlacklistService,
    IAuditService auditService)
    : IRequestHandler<ForcePasswordChangeCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(ForcePasswordChangeCommand request, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(request.UserId.ToString());
        if (user is null)
            return Result<bool>.Failure("User not found.");

        user.MustChangePassword = true;
        user.UpdatedAt = DateTime.UtcNow;

        var updateResult = await userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
            return Result<bool>.Failure(updateResult.Errors.Select(e => e.Description).ToArray());

        await tokenBlacklistService.BlacklistUserTokensAsync(user.Id);

        await auditService.LogAsync(
            AuditActions.UserUpdated,
            "User",
            user.Id.ToString(),
            "Forced password change",
            request.CurrentUserId,
            request.IpAddress,
            request.UserAgent,
            cancellationToken);

        return Result<bool>.Success(true);
    }
}
