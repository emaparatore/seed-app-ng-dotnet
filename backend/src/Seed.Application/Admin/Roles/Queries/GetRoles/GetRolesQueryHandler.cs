using MediatR;
using Microsoft.AspNetCore.Identity;
using Seed.Application.Admin.Roles.Models;
using Seed.Application.Common;
using Seed.Domain.Entities;

namespace Seed.Application.Admin.Roles.Queries.GetRoles;

public sealed class GetRolesQueryHandler(
    RoleManager<ApplicationRole> roleManager,
    UserManager<ApplicationUser> userManager)
    : IRequestHandler<GetRolesQuery, Result<IReadOnlyList<AdminRoleDto>>>
{
    public async Task<Result<IReadOnlyList<AdminRoleDto>>> Handle(
        GetRolesQuery request, CancellationToken cancellationToken)
    {
        var roles = roleManager.Roles.OrderBy(r => r.Name).ToList();

        var items = new List<AdminRoleDto>();
        foreach (var role in roles)
        {
            var usersInRole = await userManager.GetUsersInRoleAsync(role.Name!);
            items.Add(new AdminRoleDto(
                role.Id,
                role.Name!,
                role.Description,
                role.IsSystemRole,
                usersInRole.Count,
                role.CreatedAt));
        }

        return Result<IReadOnlyList<AdminRoleDto>>.Success(items);
    }
}
