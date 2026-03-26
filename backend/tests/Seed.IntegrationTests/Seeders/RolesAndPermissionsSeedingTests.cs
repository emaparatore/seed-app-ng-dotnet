using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Seed.Domain.Authorization;
using Seed.Domain.Entities;
using Seed.Infrastructure.Persistence;
using Seed.Infrastructure.Persistence.Seeders;
using Seed.IntegrationTests.Infrastructure;

namespace Seed.IntegrationTests.Seeders;

public class RolesAndPermissionsSeedingTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    [Fact]
    public async Task Seeder_Creates_All_Permissions()
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var permissions = await dbContext.Permissions.ToListAsync();
        var allPermissionNames = Permissions.GetAll();

        permissions.Should().HaveCount(allPermissionNames.Count);
        permissions.Select(p => p.Name).Should().BeEquivalentTo(allPermissionNames);
    }

    [Fact]
    public async Task Seeder_Creates_Three_System_Roles()
    {
        using var scope = factory.Services.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();

        var expectedRoles = new[] { RolesAndPermissionsSeeder.SuperAdminRole, RolesAndPermissionsSeeder.AdminRole, RolesAndPermissionsSeeder.UserRole };

        foreach (var roleName in expectedRoles)
        {
            var role = await roleManager.FindByNameAsync(roleName);
            role.Should().NotBeNull($"role {roleName} should exist");
            role!.IsSystemRole.Should().BeTrue($"role {roleName} should be a system role");
        }
    }

    [Fact]
    public async Task SuperAdmin_Has_All_Permissions()
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();

        var superAdminRole = await roleManager.FindByNameAsync(RolesAndPermissionsSeeder.SuperAdminRole);
        superAdminRole.Should().NotBeNull();

        var rolePermissions = await dbContext.RolePermissions
            .Where(rp => rp.RoleId == superAdminRole!.Id)
            .Include(rp => rp.Permission)
            .ToListAsync();

        var allPermissions = Permissions.GetAll();
        rolePermissions.Should().HaveCount(allPermissions.Count);
        rolePermissions.Select(rp => rp.Permission.Name).Should().BeEquivalentTo(allPermissions);
    }

    [Fact]
    public async Task Admin_Has_All_Permissions_Except_SettingsManage_And_RolesDelete()
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();

        var adminRole = await roleManager.FindByNameAsync(RolesAndPermissionsSeeder.AdminRole);
        adminRole.Should().NotBeNull();

        var rolePermissions = await dbContext.RolePermissions
            .Where(rp => rp.RoleId == adminRole!.Id)
            .Include(rp => rp.Permission)
            .ToListAsync();

        var expectedCount = Permissions.GetAll().Count - 2;
        rolePermissions.Should().HaveCount(expectedCount);

        var permissionNames = rolePermissions.Select(rp => rp.Permission.Name).ToList();
        permissionNames.Should().NotContain(Permissions.Settings.Manage);
        permissionNames.Should().NotContain(Permissions.Roles.Delete);
    }

    [Fact]
    public async Task User_Has_No_Permissions()
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();

        var userRole = await roleManager.FindByNameAsync(RolesAndPermissionsSeeder.UserRole);
        userRole.Should().NotBeNull();

        var rolePermissions = await dbContext.RolePermissions
            .Where(rp => rp.RoleId == userRole!.Id)
            .ToListAsync();

        rolePermissions.Should().BeEmpty();
    }

    [Fact]
    public async Task Seeder_Is_Idempotent()
    {
        // Run seeder a second time
        using var scope = factory.Services.CreateScope();
        var seeder = scope.ServiceProvider.GetRequiredService<RolesAndPermissionsSeeder>();
        await seeder.SeedAsync();

        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Verify no duplicates
        var permissions = await dbContext.Permissions.ToListAsync();
        permissions.Should().HaveCount(Permissions.GetAll().Count);
        permissions.Select(p => p.Name).Should().OnlyHaveUniqueItems();

        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
        var superAdmin = await roleManager.FindByNameAsync(RolesAndPermissionsSeeder.SuperAdminRole);
        var superAdminPermissions = await dbContext.RolePermissions
            .Where(rp => rp.RoleId == superAdmin!.Id)
            .ToListAsync();
        superAdminPermissions.Should().HaveCount(Permissions.GetAll().Count);
    }

    [Fact]
    public async Task Permissions_Have_Correct_Categories()
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var permissions = await dbContext.Permissions.ToListAsync();

        foreach (var permission in permissions)
        {
            var expectedCategory = permission.Name.Split('.')[0];
            permission.Category.Should().Be(expectedCategory,
                $"permission {permission.Name} should have category {expectedCategory}");
        }
    }
}
