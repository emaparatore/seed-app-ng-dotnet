using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Seed.Domain.Authorization;
using Seed.Domain.Entities;

namespace Seed.Infrastructure.Persistence.Seeders;

public class RolesAndPermissionsSeeder
{
    private readonly ApplicationDbContext _dbContext;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly ILogger<RolesAndPermissionsSeeder> _logger;

    public const string SuperAdminRole = SystemRoles.SuperAdmin;
    public const string AdminRole = SystemRoles.Admin;
    public const string UserRole = SystemRoles.User;

    private static readonly Dictionary<string, string> RoleDescriptions = new()
    {
        [SuperAdminRole] = "Full system access with all permissions",
        [AdminRole] = "Administrative access with most permissions",
        [UserRole] = "Standard user with no administrative permissions"
    };

    /// <summary>
    /// Permissions excluded from the Admin role.
    /// </summary>
    private static readonly HashSet<string> AdminExcludedPermissions =
    [
        Permissions.Settings.Manage,
        Permissions.Roles.Delete
    ];

    public RolesAndPermissionsSeeder(
        ApplicationDbContext dbContext,
        RoleManager<ApplicationRole> roleManager,
        ILogger<RolesAndPermissionsSeeder> logger)
    {
        _dbContext = dbContext;
        _roleManager = roleManager;
        _logger = logger;
    }

    public async Task SeedAsync()
    {
        await SeedPermissionsAsync();
        await SeedRolesAsync();
        await SeedRolePermissionsAsync();
    }

    private async Task SeedPermissionsAsync()
    {
        var existingPermissions = await _dbContext.Permissions
            .Select(p => p.Name)
            .ToHashSetAsync();

        var allPermissions = Permissions.GetAll();
        var newPermissions = allPermissions
            .Where(p => !existingPermissions.Contains(p))
            .Select(p => new Permission
            {
                Id = Guid.NewGuid(),
                Name = p,
                Description = p,
                Category = p.Split('.')[0]
            })
            .ToList();

        if (newPermissions.Count > 0)
        {
            _dbContext.Permissions.AddRange(newPermissions);
            await _dbContext.SaveChangesAsync();
            _logger.LogInformation("Seeded {Count} permissions", newPermissions.Count);
        }
        else
        {
            _logger.LogDebug("All permissions already exist, skipping");
        }
    }

    private async Task SeedRolesAsync()
    {
        var systemRoles = new[] { SuperAdminRole, AdminRole, UserRole };

        foreach (var roleName in systemRoles)
        {
            if (await _roleManager.RoleExistsAsync(roleName))
            {
                _logger.LogDebug("Role {Role} already exists, skipping", roleName);
                continue;
            }

            var role = new ApplicationRole
            {
                Name = roleName,
                Description = RoleDescriptions[roleName],
                IsSystemRole = true,
                CreatedAt = DateTime.UtcNow
            };

            var result = await _roleManager.CreateAsync(role);
            if (result.Succeeded)
            {
                _logger.LogInformation("Created system role {Role}", roleName);
            }
            else
            {
                _logger.LogError("Failed to create role {Role}: {Errors}",
                    roleName, string.Join(", ", result.Errors.Select(e => e.Description)));
            }
        }
    }

    private async Task SeedRolePermissionsAsync()
    {
        var permissions = await _dbContext.Permissions.ToListAsync();
        var permissionsByName = permissions.ToDictionary(p => p.Name);

        await AssignRolePermissionsAsync(SuperAdminRole, permissions.Select(p => p.Name), permissionsByName);
        await AssignRolePermissionsAsync(AdminRole,
            permissions.Select(p => p.Name).Where(p => !AdminExcludedPermissions.Contains(p)),
            permissionsByName);
        // User role gets no permissions
    }

    private async Task AssignRolePermissionsAsync(
        string roleName,
        IEnumerable<string> permissionNames,
        Dictionary<string, Permission> permissionsByName)
    {
        var role = await _roleManager.FindByNameAsync(roleName);
        if (role is null)
        {
            _logger.LogWarning("Role {Role} not found, cannot assign permissions", roleName);
            return;
        }

        var existingPermissionIds = await _dbContext.RolePermissions
            .Where(rp => rp.RoleId == role.Id)
            .Select(rp => rp.PermissionId)
            .ToHashSetAsync();

        var newAssignments = permissionNames
            .Where(pn => permissionsByName.ContainsKey(pn))
            .Select(pn => permissionsByName[pn])
            .Where(p => !existingPermissionIds.Contains(p.Id))
            .Select(p => new RolePermission
            {
                RoleId = role.Id,
                PermissionId = p.Id
            })
            .ToList();

        if (newAssignments.Count > 0)
        {
            _dbContext.RolePermissions.AddRange(newAssignments);
            await _dbContext.SaveChangesAsync();
            _logger.LogInformation("Assigned {Count} permissions to role {Role}",
                newAssignments.Count, roleName);
        }
        else
        {
            _logger.LogDebug("Role {Role} already has all required permissions", roleName);
        }
    }
}
