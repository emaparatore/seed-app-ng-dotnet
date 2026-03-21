using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Seed.Domain.Authorization;
using Seed.Domain.Entities;
using Seed.Infrastructure.Persistence.Seeders;
using Seed.Shared.Configuration;

namespace Seed.UnitTests.Seeders;

public class SuperAdminSeederTests
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<SuperAdminSeeder> _logger;

    public SuperAdminSeederTests()
    {
        var store = Substitute.For<IUserStore<ApplicationUser>>();
        _userManager = Substitute.For<UserManager<ApplicationUser>>(
            store, null, null, null, null, null, null, null, null);
        _logger = Substitute.For<ILogger<SuperAdminSeeder>>();
    }

    private SuperAdminSeeder CreateSeeder(SuperAdminSettings? settings = null)
    {
        settings ??= new SuperAdminSettings
        {
            Email = "admin@test.com",
            Password = "Admin123!",
            FirstName = "Super",
            LastName = "Admin"
        };
        return new SuperAdminSeeder(_userManager, Options.Create(settings), _logger);
    }

    [Fact]
    public async Task SeedAsync_CreatesUser_WhenNoSuperAdminExists()
    {
        _userManager.GetUsersInRoleAsync(SystemRoles.SuperAdmin)
            .Returns(new List<ApplicationUser>());
        _userManager.CreateAsync(Arg.Any<ApplicationUser>(), Arg.Any<string>())
            .Returns(IdentityResult.Success);
        _userManager.AddToRoleAsync(Arg.Any<ApplicationUser>(), Arg.Any<string>())
            .Returns(IdentityResult.Success);

        var seeder = CreateSeeder();
        await seeder.SeedAsync();

        await _userManager.Received(1).CreateAsync(Arg.Any<ApplicationUser>(), "Admin123!");
        await _userManager.Received(1).AddToRoleAsync(Arg.Any<ApplicationUser>(), SystemRoles.SuperAdmin);
    }

    [Fact]
    public async Task SeedAsync_SkipsCreation_WhenSuperAdminExists()
    {
        _userManager.GetUsersInRoleAsync(SystemRoles.SuperAdmin)
            .Returns(new List<ApplicationUser> { new() { Email = "existing@test.com" } });

        var seeder = CreateSeeder();
        await seeder.SeedAsync();

        await _userManager.DidNotReceive().CreateAsync(Arg.Any<ApplicationUser>(), Arg.Any<string>());
    }

    [Fact]
    public async Task SeedAsync_SkipsCreation_WhenEmailNotConfigured()
    {
        var settings = new SuperAdminSettings { Email = "", Password = "Admin123!" };
        var seeder = CreateSeeder(settings);

        await seeder.SeedAsync();

        await _userManager.DidNotReceive().CreateAsync(Arg.Any<ApplicationUser>(), Arg.Any<string>());
    }

    [Fact]
    public async Task SeedAsync_SetsCorrectUserProperties()
    {
        ApplicationUser? capturedUser = null;
        _userManager.GetUsersInRoleAsync(SystemRoles.SuperAdmin)
            .Returns(new List<ApplicationUser>());
        _userManager.CreateAsync(Arg.Do<ApplicationUser>(u => capturedUser = u), Arg.Any<string>())
            .Returns(IdentityResult.Success);
        _userManager.AddToRoleAsync(Arg.Any<ApplicationUser>(), Arg.Any<string>())
            .Returns(IdentityResult.Success);

        var seeder = CreateSeeder();
        await seeder.SeedAsync();

        capturedUser.Should().NotBeNull();
        capturedUser!.Email.Should().Be("admin@test.com");
        capturedUser.FirstName.Should().Be("Super");
        capturedUser.LastName.Should().Be("Admin");
        capturedUser.MustChangePassword.Should().BeTrue();
        capturedUser.EmailConfirmed.Should().BeTrue();
        capturedUser.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task SeedAsync_LogsWarning_WhenCreateFails()
    {
        _userManager.GetUsersInRoleAsync(SystemRoles.SuperAdmin)
            .Returns(new List<ApplicationUser>());
        _userManager.CreateAsync(Arg.Any<ApplicationUser>(), Arg.Any<string>())
            .Returns(IdentityResult.Failed(new IdentityError { Description = "Password too weak" }));

        var seeder = CreateSeeder();
        await seeder.SeedAsync();

        await _userManager.DidNotReceive().AddToRoleAsync(Arg.Any<ApplicationUser>(), Arg.Any<string>());
    }
}
