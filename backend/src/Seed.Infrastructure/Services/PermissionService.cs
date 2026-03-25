using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Seed.Application.Admin.Roles.Models;
using Seed.Application.Common.Interfaces;
using Seed.Domain.Entities;
using Seed.Infrastructure.Persistence;

namespace Seed.Infrastructure.Services;

public sealed class PermissionService(
    ApplicationDbContext dbContext,
    UserManager<ApplicationUser> userManager,
    IDistributedCache cache) : IPermissionService
{
    private const string CacheKeyPrefix = "permissions:user:";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    public async Task<IReadOnlySet<string>> GetPermissionsAsync(Guid userId)
    {
        var cacheKey = $"{CacheKeyPrefix}{userId}";

        var cached = await cache.GetStringAsync(cacheKey);
        if (cached is not null)
        {
            var cachedPermissions = JsonSerializer.Deserialize<HashSet<string>>(cached)!;
            return cachedPermissions;
        }

        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
            return new HashSet<string>();

        var roleNames = await userManager.GetRolesAsync(user);
        if (roleNames.Count == 0)
            return new HashSet<string>();

        var permissions = await dbContext.RolePermissions
            .Include(rp => rp.Role)
            .Include(rp => rp.Permission)
            .Where(rp => roleNames.Contains(rp.Role.Name!))
            .Select(rp => rp.Permission.Name)
            .Distinct()
            .ToListAsync();

        var permissionSet = new HashSet<string>(permissions);

        await cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(permissionSet), new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = CacheDuration
        });

        return permissionSet;
    }

    public async Task InvalidateUserPermissionsCacheAsync(Guid userId)
    {
        var cacheKey = $"{CacheKeyPrefix}{userId}";
        await cache.RemoveAsync(cacheKey);
    }

    public async Task<IReadOnlyList<PermissionDto>> GetAllPermissionsAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.Permissions
            .OrderBy(p => p.Category)
            .ThenBy(p => p.Name)
            .Select(p => new PermissionDto(p.Id, p.Name, p.Description, p.Category))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<string>> GetRolePermissionNamesAsync(Guid roleId, CancellationToken cancellationToken = default)
    {
        return await dbContext.RolePermissions
            .Where(rp => rp.RoleId == roleId)
            .Include(rp => rp.Permission)
            .Select(rp => rp.Permission.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task SetRolePermissionsAsync(Guid roleId, IEnumerable<string> permissionNames, CancellationToken cancellationToken = default)
    {
        // Remove existing
        var existing = await dbContext.RolePermissions
            .Where(rp => rp.RoleId == roleId)
            .ToListAsync(cancellationToken);
        dbContext.RolePermissions.RemoveRange(existing);

        // Add new
        var permissionNameList = permissionNames.ToList();
        if (permissionNameList.Count > 0)
        {
            var permissions = await dbContext.Permissions
                .Where(p => permissionNameList.Contains(p.Name))
                .ToListAsync(cancellationToken);

            foreach (var permission in permissions)
            {
                dbContext.RolePermissions.Add(new RolePermission
                {
                    RoleId = roleId,
                    PermissionId = permission.Id
                });
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveAllRolePermissionsAsync(Guid roleId, CancellationToken cancellationToken = default)
    {
        var existing = await dbContext.RolePermissions
            .Where(rp => rp.RoleId == roleId)
            .ToListAsync(cancellationToken);
        dbContext.RolePermissions.RemoveRange(existing);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
