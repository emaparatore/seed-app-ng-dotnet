using MediatR;
using Microsoft.AspNetCore.Identity;
using Seed.Application.Common;
using Seed.Application.Common.Interfaces;
using Seed.Domain.Authorization;
using Seed.Domain.Entities;

namespace Seed.Application.Admin.Users.Commands.ToggleUserStatus;

public sealed class ToggleUserStatusCommandHandler(
    UserManager<ApplicationUser> userManager,
    ITokenBlacklistService tokenBlacklistService,
    IAuditService auditService)
    : IRequestHandler<ToggleUserStatusCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(ToggleUserStatusCommand request, CancellationToken cancellationToken)
    {
        if (request.UserId == request.CurrentUserId)
            return Result<bool>.Failure("You cannot change your own status.");

        var user = await userManager.FindByIdAsync(request.UserId.ToString());
        if (user is null)
            return Result<bool>.Failure("User not found.");

        // Check if user is SuperAdmin
        var roles = await userManager.GetRolesAsync(user);
        if (roles.Contains(SystemRoles.SuperAdmin))
            return Result<bool>.Failure("Cannot change the status of a SuperAdmin user.");

        var previousStatus = user.IsActive;
        user.IsActive = request.IsActive;
        user.UpdatedAt = DateTime.UtcNow;

        var updateResult = await userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
            return Result<bool>.Failure(updateResult.Errors.Select(e => e.Description).ToArray());

        // Blacklist tokens when deactivating
        if (!request.IsActive)
            await tokenBlacklistService.BlacklistUserTokensAsync(user.Id);

        await auditService.LogAsync(
            AuditActions.UserStatusChanged,
            "User",
            user.Id.ToString(),
            $"Status changed from {(previousStatus ? "Active" : "Inactive")} to {(request.IsActive ? "Active" : "Inactive")}",
            request.CurrentUserId,
            request.IpAddress,
            request.UserAgent,
            cancellationToken);

        return Result<bool>.Success(true);
    }
}
