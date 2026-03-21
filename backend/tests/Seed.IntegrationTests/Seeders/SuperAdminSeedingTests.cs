using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Seed.Domain.Authorization;
using Seed.Domain.Entities;
using Seed.Infrastructure.Persistence;
using Seed.Infrastructure.Persistence.Seeders;
using Seed.IntegrationTests.Infrastructure;
using Seed.Shared.Configuration;

namespace Seed.IntegrationTests.Seeders;

public class SuperAdminSeedingTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    [Fact]
    public async Task SuperAdmin_Is_Created_With_Correct_Data()
    {
        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var superAdmins = await userManager.GetUsersInRoleAsync(SystemRoles.SuperAdmin);

        // The factory runs in Development mode with appsettings.Development.json,
        // so the SuperAdmin seeder should have created a user
        superAdmins.Should().HaveCount(1);
        var admin = superAdmins[0];
        admin.Email.Should().Be("admin@seedapp.local");
        admin.FirstName.Should().Be("Super");
        admin.LastName.Should().Be("Admin");
        admin.EmailConfirmed.Should().BeTrue();
    }

    [Fact]
    public async Task SuperAdmin_Has_SuperAdmin_Role()
    {
        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var superAdmins = await userManager.GetUsersInRoleAsync(SystemRoles.SuperAdmin);
        superAdmins.Should().NotBeEmpty();

        var roles = await userManager.GetRolesAsync(superAdmins[0]);
        roles.Should().Contain(SystemRoles.SuperAdmin);
    }

    [Fact]
    public async Task SuperAdmin_Has_MustChangePassword_True()
    {
        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var superAdmins = await userManager.GetUsersInRoleAsync(SystemRoles.SuperAdmin);
        superAdmins.Should().NotBeEmpty();

        superAdmins[0].MustChangePassword.Should().BeTrue();
    }

    [Fact]
    public async Task Seeder_Is_Idempotent()
    {
        using var scope = factory.Services.CreateScope();
        var seeder = scope.ServiceProvider.GetRequiredService<SuperAdminSeeder>();

        // Run seeder a second time
        await seeder.SeedAsync();

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var superAdmins = await userManager.GetUsersInRoleAsync(SystemRoles.SuperAdmin);
        superAdmins.Should().HaveCount(1);
    }

    [Fact]
    public async Task Seeder_Skips_When_No_Email_Configured()
    {
        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        // Create a seeder with empty settings
        var emptySettings = Options.Create(new SuperAdminSettings());
        var logger = scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<SuperAdminSeeder>>();
        var emptySeeder = new SuperAdminSeeder(userManager, emptySettings, logger);

        // Get count before
        var beforeCount = (await userManager.GetUsersInRoleAsync(SystemRoles.SuperAdmin)).Count;

        await emptySeeder.SeedAsync();

        // Count should not have changed
        var afterCount = (await userManager.GetUsersInRoleAsync(SystemRoles.SuperAdmin)).Count;
        afterCount.Should().Be(beforeCount);
    }
}
