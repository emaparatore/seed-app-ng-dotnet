using System.Text.Json;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Seed.Application.Common;
using Seed.Application.Common.Interfaces;
using Seed.Domain.Authorization;
using Seed.Domain.Entities;

namespace Seed.Application.Admin.Users.Commands.AssignUserRoles;

public sealed class AssignUserRolesCommandHandler(
    UserManager<ApplicationUser> userManager,
    RoleManager<ApplicationRole> roleManager,
    IPermissionService permissionService,
    ITokenBlacklistService tokenBlacklistService,
    IAuditService auditService)
    : IRequestHandler<AssignUserRolesCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(AssignUserRolesCommand request, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(request.UserId.ToString());
        if (user is null)
            return Result<bool>.Failure("User not found.");

        var currentRoles = await userManager.GetRolesAsync(user);

        // Prevent modifying SuperAdmin's roles
        if (currentRoles.Contains(SystemRoles.SuperAdmin))
            return Result<bool>.Failure("Cannot modify the roles of a SuperAdmin user.");

        // Prevent assigning SuperAdmin role
        if (request.RoleNames.Contains(SystemRoles.SuperAdmin, StringComparer.OrdinalIgnoreCase))
            return Result<bool>.Failure("Cannot assign the SuperAdmin role.");

        // Validate all roles exist
        foreach (var roleName in request.RoleNames)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
                return Result<bool>.Failure($"Role '{roleName}' does not exist.");
        }

        // Remove old roles
        var removeResult = await userManager.RemoveFromRolesAsync(user, currentRoles);
        if (!removeResult.Succeeded)
            return Result<bool>.Failure(removeResult.Errors.Select(e => e.Description).ToArray());

        // Add new roles
        if (request.RoleNames.Length > 0)
        {
            var addResult = await userManager.AddToRolesAsync(user, request.RoleNames);
            if (!addResult.Succeeded)
                return Result<bool>.Failure(addResult.Errors.Select(e => e.Description).ToArray());
        }

        user.UpdatedAt = DateTime.UtcNow;
        await userManager.UpdateAsync(user);

        // Invalidate permission cache and blacklist tokens
        await permissionService.InvalidateUserPermissionsCacheAsync(user.Id);
        await tokenBlacklistService.BlacklistUserTokensAsync(user.Id);

        await auditService.LogAsync(
            AuditActions.UserRolesChanged,
            "User",
            user.Id.ToString(),
            JsonSerializer.Serialize(new { before = currentRoles, after = request.RoleNames }),
            request.CurrentUserId,
            request.IpAddress,
            request.UserAgent,
            cancellationToken);

        return Result<bool>.Success(true);
    }
}
