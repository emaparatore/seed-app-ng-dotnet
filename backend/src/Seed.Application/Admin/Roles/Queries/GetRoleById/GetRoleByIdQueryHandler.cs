using MediatR;
using Microsoft.AspNetCore.Identity;
using Seed.Application.Admin.Roles.Models;
using Seed.Application.Common;
using Seed.Application.Common.Interfaces;
using Seed.Domain.Entities;

namespace Seed.Application.Admin.Roles.Queries.GetRoleById;

public sealed class GetRoleByIdQueryHandler(
    RoleManager<ApplicationRole> roleManager,
    UserManager<ApplicationUser> userManager,
    IPermissionService permissionService)
    : IRequestHandler<GetRoleByIdQuery, Result<AdminRoleDetailDto>>
{
    public async Task<Result<AdminRoleDetailDto>> Handle(
        GetRoleByIdQuery request, CancellationToken cancellationToken)
    {
        var role = await roleManager.FindByIdAsync(request.RoleId.ToString());
        if (role is null)
            return Result<AdminRoleDetailDto>.Failure("Role not found.");

        var usersInRole = await userManager.GetUsersInRoleAsync(role.Name!);
        var permissions = await permissionService.GetRolePermissionNamesAsync(role.Id, cancellationToken);

        var dto = new AdminRoleDetailDto(
            role.Id,
            role.Name!,
            role.Description,
            role.IsSystemRole,
            usersInRole.Count,
            role.CreatedAt,
            permissions);

        return Result<AdminRoleDetailDto>.Success(dto);
    }
}
