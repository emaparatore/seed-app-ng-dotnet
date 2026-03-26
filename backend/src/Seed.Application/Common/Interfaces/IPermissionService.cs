using Seed.Application.Admin.Roles.Models;

namespace Seed.Application.Common.Interfaces;

public interface IPermissionService
{
    Task<IReadOnlySet<string>> GetPermissionsAsync(Guid userId);
    Task InvalidateUserPermissionsCacheAsync(Guid userId);
    Task<IReadOnlyList<PermissionDto>> GetAllPermissionsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> GetRolePermissionNamesAsync(Guid roleId, CancellationToken cancellationToken = default);
    Task SetRolePermissionsAsync(Guid roleId, IEnumerable<string> permissionNames, CancellationToken cancellationToken = default);
    Task RemoveAllRolePermissionsAsync(Guid roleId, CancellationToken cancellationToken = default);
}
