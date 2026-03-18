namespace Seed.Application.Common.Interfaces;

public interface IPermissionService
{
    Task<IReadOnlySet<string>> GetPermissionsAsync(Guid userId);
    Task InvalidateUserPermissionsCacheAsync(Guid userId);
}
