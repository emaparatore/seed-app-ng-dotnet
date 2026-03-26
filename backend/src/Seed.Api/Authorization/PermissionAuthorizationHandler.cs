using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Seed.Application.Common.Interfaces;
using Seed.Domain.Authorization;

namespace Seed.Api.Authorization;

public class PermissionAuthorizationHandler(IPermissionService permissionService)
    : AuthorizationHandler<PermissionRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
            return;

        // SuperAdmin bypasses all permission checks
        if (context.User.IsInRole(SystemRoles.SuperAdmin))
        {
            context.Succeed(requirement);
            return;
        }

        var permissions = await permissionService.GetPermissionsAsync(Guid.Parse(userId));
        if (permissions.Contains(requirement.Permission))
        {
            context.Succeed(requirement);
        }
    }
}
