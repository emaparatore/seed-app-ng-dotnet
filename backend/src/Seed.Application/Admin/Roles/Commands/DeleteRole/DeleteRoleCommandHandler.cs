using MediatR;
using Microsoft.AspNetCore.Identity;
using Seed.Application.Common;
using Seed.Application.Common.Interfaces;
using Seed.Domain.Authorization;
using Seed.Domain.Entities;

namespace Seed.Application.Admin.Roles.Commands.DeleteRole;

public sealed class DeleteRoleCommandHandler(
    RoleManager<ApplicationRole> roleManager,
    UserManager<ApplicationUser> userManager,
    IPermissionService permissionService,
    IAuditService auditService)
    : IRequestHandler<DeleteRoleCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(DeleteRoleCommand request, CancellationToken cancellationToken)
    {
        var role = await roleManager.FindByIdAsync(request.RoleId.ToString());
        if (role is null)
            return Result<bool>.Failure("Role not found.");

        if (role.IsSystemRole)
            return Result<bool>.Failure("Cannot delete a system role.");

        var usersInRole = await userManager.GetUsersInRoleAsync(role.Name!);
        if (usersInRole.Count > 0)
            return Result<bool>.Failure("Cannot delete a role that has users assigned.");

        // Remove role permissions
        await permissionService.RemoveAllRolePermissionsAsync(role.Id, cancellationToken);

        // Delete role
        var deleteResult = await roleManager.DeleteAsync(role);
        if (!deleteResult.Succeeded)
            return Result<bool>.Failure(deleteResult.Errors.Select(e => e.Description).ToArray());

        await auditService.LogAsync(
            AuditActions.RoleDeleted,
            "Role",
            role.Id.ToString(),
            $"Name: {role.Name}",
            request.CurrentUserId,
            request.IpAddress,
            request.UserAgent,
            cancellationToken);

        return Result<bool>.Success(true);
    }
}
