using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using NSubstitute;
using Seed.Application.Admin.Roles.Commands.UpdateRole;
using Seed.Application.Common.Interfaces;
using Seed.Domain.Authorization;
using Seed.Domain.Entities;

namespace Seed.UnitTests.Admin.Roles;

public class UpdateRoleCommandHandlerTests
{
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IPermissionService _permissionService;
    private readonly ITokenBlacklistService _tokenBlacklistService;
    private readonly IAuditService _auditService;
    private readonly UpdateRoleCommandHandler _handler;

    public UpdateRoleCommandHandlerTests()
    {
        var roleStore = Substitute.For<IRoleStore<ApplicationRole>>();
        _roleManager = Substitute.For<RoleManager<ApplicationRole>>(
            roleStore, null, null, null, null);

        var userStore = Substitute.For<IUserStore<ApplicationUser>>();
        _userManager = Substitute.For<UserManager<ApplicationUser>>(
            userStore, null, null, null, null, null, null, null, null);

        _permissionService = Substitute.For<IPermissionService>();
        _tokenBlacklistService = Substitute.For<ITokenBlacklistService>();
        _auditService = Substitute.For<IAuditService>();
        _handler = new UpdateRoleCommandHandler(
            _roleManager, _userManager, _permissionService, _tokenBlacklistService, _auditService);
    }

    [Fact]
    public async Task Should_Update_Role_Successfully()
    {
        var roleId = Guid.NewGuid();
        var role = new ApplicationRole { Id = roleId, Name = "Editor", IsSystemRole = false };
        var command = new UpdateRoleCommand("Editor Updated", "New desc", ["Users.Read"])
        {
            RoleId = roleId, CurrentUserId = Guid.NewGuid()
        };

        _roleManager.FindByIdAsync(roleId.ToString()).Returns(role);
        _roleManager.RoleExistsAsync("Editor Updated").Returns(false);
        _roleManager.UpdateAsync(role).Returns(IdentityResult.Success);
        _permissionService.GetRolePermissionNamesAsync(roleId, Arg.Any<CancellationToken>())
            .Returns(new List<string> { "Users.Read" });
        _userManager.GetUsersInRoleAsync(Arg.Any<string>()).Returns(new List<ApplicationUser>());

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        await _auditService.Received(1).LogAsync(
            AuditActions.RoleUpdated, "Role",
            roleId.ToString(), Arg.Any<string?>(), Arg.Any<Guid?>(),
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_Fail_When_Role_Not_Found()
    {
        var command = new UpdateRoleCommand("X", null, [])
        {
            RoleId = Guid.NewGuid(), CurrentUserId = Guid.NewGuid()
        };
        _roleManager.FindByIdAsync(command.RoleId.ToString()).Returns((ApplicationRole?)null);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("Role not found.");
    }

    [Fact]
    public async Task Should_Block_SuperAdmin_Modification()
    {
        var roleId = Guid.NewGuid();
        var role = new ApplicationRole { Id = roleId, Name = SystemRoles.SuperAdmin, IsSystemRole = true };
        var command = new UpdateRoleCommand("SuperAdmin", null, ["Users.Read"])
        {
            RoleId = roleId, CurrentUserId = Guid.NewGuid()
        };

        _roleManager.FindByIdAsync(roleId.ToString()).Returns(role);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("Cannot modify the SuperAdmin role permissions.");
    }

    [Fact]
    public async Task Should_Fail_When_Duplicate_Name()
    {
        var roleId = Guid.NewGuid();
        var role = new ApplicationRole { Id = roleId, Name = "Editor", IsSystemRole = false };
        var command = new UpdateRoleCommand("Admin", null, [])
        {
            RoleId = roleId, CurrentUserId = Guid.NewGuid()
        };

        _roleManager.FindByIdAsync(roleId.ToString()).Returns(role);
        _roleManager.RoleExistsAsync("Admin").Returns(true);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("A role with this name already exists.");
    }

    [Fact]
    public async Task Should_Invalidate_Cache_When_Permissions_Change()
    {
        var roleId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var role = new ApplicationRole { Id = roleId, Name = "Editor", IsSystemRole = false };
        var user = new ApplicationUser { Id = userId, Email = "user@test.com" };
        var command = new UpdateRoleCommand("Editor", null, ["Users.Read", "Users.Create"])
        {
            RoleId = roleId, CurrentUserId = Guid.NewGuid()
        };

        _roleManager.FindByIdAsync(roleId.ToString()).Returns(role);
        _roleManager.UpdateAsync(role).Returns(IdentityResult.Success);
        _permissionService.GetRolePermissionNamesAsync(roleId, Arg.Any<CancellationToken>())
            .Returns(new List<string> { "Users.Read" });
        _userManager.GetUsersInRoleAsync("Editor").Returns(new List<ApplicationUser> { user });

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        await _permissionService.Received(1).InvalidateUserPermissionsCacheAsync(userId);
        await _tokenBlacklistService.Received(1).BlacklistUserTokensAsync(userId);
    }

    [Fact]
    public async Task Should_Not_Invalidate_Cache_When_Permissions_Unchanged()
    {
        var roleId = Guid.NewGuid();
        var role = new ApplicationRole { Id = roleId, Name = "Editor", IsSystemRole = false };
        var command = new UpdateRoleCommand("Editor Renamed", null, ["Users.Read"])
        {
            RoleId = roleId, CurrentUserId = Guid.NewGuid()
        };

        _roleManager.FindByIdAsync(roleId.ToString()).Returns(role);
        _roleManager.RoleExistsAsync("Editor Renamed").Returns(false);
        _roleManager.UpdateAsync(role).Returns(IdentityResult.Success);
        _permissionService.GetRolePermissionNamesAsync(roleId, Arg.Any<CancellationToken>())
            .Returns(new List<string> { "Users.Read" });
        _userManager.GetUsersInRoleAsync(Arg.Any<string>()).Returns(new List<ApplicationUser>());

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        await _permissionService.DidNotReceive().InvalidateUserPermissionsCacheAsync(Arg.Any<Guid>());
        await _tokenBlacklistService.DidNotReceive().BlacklistUserTokensAsync(Arg.Any<Guid>());
    }
}
