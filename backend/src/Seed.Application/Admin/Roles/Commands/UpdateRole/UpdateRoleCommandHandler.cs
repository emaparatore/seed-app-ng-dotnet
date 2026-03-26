using System.Text.Json;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Seed.Application.Common;
using Seed.Application.Common.Interfaces;
using Seed.Domain.Authorization;
using Seed.Domain.Entities;

namespace Seed.Application.Admin.Roles.Commands.UpdateRole;

public sealed class UpdateRoleCommandHandler(
    RoleManager<ApplicationRole> roleManager,
    UserManager<ApplicationUser> userManager,
    IPermissionService permissionService,
    ITokenBlacklistService tokenBlacklistService,
    IAuditService auditService)
    : IRequestHandler<UpdateRoleCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(UpdateRoleCommand request, CancellationToken cancellationToken)
    {
        var role = await roleManager.FindByIdAsync(request.RoleId.ToString());
        if (role is null)
            return Result<bool>.Failure("Role not found.");

        // Block permission changes for SuperAdmin
        if (role.IsSystemRole && role.Name == SystemRoles.SuperAdmin)
            return Result<bool>.Failure("Cannot modify the SuperAdmin role permissions.");

        // Check for duplicate name (if changed)
        if (!string.Equals(role.Name, request.Name, StringComparison.OrdinalIgnoreCase))
        {
            if (await roleManager.RoleExistsAsync(request.Name))
                return Result<bool>.Failure("A role with this name already exists.");
        }

        // Get old state for audit
        var oldPermissions = await permissionService.GetRolePermissionNamesAsync(role.Id, cancellationToken);
        var oldName = role.Name;

        // Update role properties
        role.Name = request.Name;
        role.Description = request.Description;

        var updateResult = await roleManager.UpdateAsync(role);
        if (!updateResult.Succeeded)
            return Result<bool>.Failure(updateResult.Errors.Select(e => e.Description).ToArray());

        // Update permissions
        await permissionService.SetRolePermissionsAsync(role.Id, request.PermissionNames, cancellationToken);

        // Check if permissions changed
        var newPermissionsSorted = request.PermissionNames.OrderBy(p => p).ToList();
        var oldPermissionsSorted = oldPermissions.OrderBy(p => p).ToList();
        var permissionsChanged = !newPermissionsSorted.SequenceEqual(oldPermissionsSorted);

        // If permissions changed, invalidate cache and blacklist tokens for all users in this role
        if (permissionsChanged)
        {
            var usersInRole = await userManager.GetUsersInRoleAsync(role.Name!);
            foreach (var user in usersInRole)
            {
                await permissionService.InvalidateUserPermissionsCacheAsync(user.Id);
                await tokenBlacklistService.BlacklistUserTokensAsync(user.Id);
            }
        }

        await auditService.LogAsync(
            AuditActions.RoleUpdated,
            "Role",
            role.Id.ToString(),
            JsonSerializer.Serialize(new
            {
                before = new { name = oldName, permissions = oldPermissionsSorted },
                after = new { name = request.Name, permissions = newPermissionsSorted }
            }),
            request.CurrentUserId,
            request.IpAddress,
            request.UserAgent,
            cancellationToken);

        return Result<bool>.Success(true);
    }
}
