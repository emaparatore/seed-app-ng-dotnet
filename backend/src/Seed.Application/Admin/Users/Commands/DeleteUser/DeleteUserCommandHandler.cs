using MediatR;
using Microsoft.AspNetCore.Identity;
using Seed.Application.Common;
using Seed.Application.Common.Interfaces;
using Seed.Domain.Authorization;
using Seed.Domain.Entities;

namespace Seed.Application.Admin.Users.Commands.DeleteUser;

public sealed class DeleteUserCommandHandler(
    UserManager<ApplicationUser> userManager,
    ITokenBlacklistService tokenBlacklistService,
    IAuditService auditService)
    : IRequestHandler<DeleteUserCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(DeleteUserCommand request, CancellationToken cancellationToken)
    {
        if (request.UserId == request.CurrentUserId)
            return Result<bool>.Failure("You cannot delete your own account.");

        var user = await userManager.FindByIdAsync(request.UserId.ToString());
        if (user is null)
            return Result<bool>.Failure("User not found.");

        // Check if user is SuperAdmin
        var roles = await userManager.GetRolesAsync(user);
        if (roles.Contains(SystemRoles.SuperAdmin))
            return Result<bool>.Failure("Cannot delete a SuperAdmin user.");

        // Soft delete
        user.IsDeleted = true;
        user.DeletedAt = DateTime.UtcNow;
        user.IsActive = false;
        user.UpdatedAt = DateTime.UtcNow;

        var updateResult = await userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
            return Result<bool>.Failure(updateResult.Errors.Select(e => e.Description).ToArray());

        // Blacklist tokens
        await tokenBlacklistService.BlacklistUserTokensAsync(user.Id);

        await auditService.LogAsync(
            AuditActions.UserDeleted,
            "User",
            user.Id.ToString(),
            $"Email: {user.Email}",
            request.CurrentUserId,
            request.IpAddress,
            request.UserAgent,
            cancellationToken);

        return Result<bool>.Success(true);
    }
}
