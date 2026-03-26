using MediatR;
using Seed.Application.Admin.Roles.Models;
using Seed.Application.Common;
using Seed.Application.Common.Interfaces;

namespace Seed.Application.Admin.Roles.Queries.GetPermissions;

public sealed class GetPermissionsQueryHandler(
    IPermissionService permissionService)
    : IRequestHandler<GetPermissionsQuery, Result<IReadOnlyList<PermissionDto>>>
{
    public async Task<Result<IReadOnlyList<PermissionDto>>> Handle(
        GetPermissionsQuery request, CancellationToken cancellationToken)
    {
        var permissions = await permissionService.GetAllPermissionsAsync(cancellationToken);
        return Result<IReadOnlyList<PermissionDto>>.Success(permissions);
    }
}
