using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using NSubstitute;
using Seed.Application.Admin.Roles.Commands.DeleteRole;
using Seed.Application.Common.Interfaces;
using Seed.Domain.Authorization;
using Seed.Domain.Entities;

namespace Seed.UnitTests.Admin.Roles;

public class DeleteRoleCommandHandlerTests
{
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IPermissionService _permissionService;
    private readonly IAuditService _auditService;
    private readonly DeleteRoleCommandHandler _handler;

    public DeleteRoleCommandHandlerTests()
    {
        var roleStore = Substitute.For<IRoleStore<ApplicationRole>>();
        _roleManager = Substitute.For<RoleManager<ApplicationRole>>(
            roleStore, null, null, null, null);

        var userStore = Substitute.For<IUserStore<ApplicationUser>>();
        _userManager = Substitute.For<UserManager<ApplicationUser>>(
            userStore, null, null, null, null, null, null, null, null);

        _permissionService = Substitute.For<IPermissionService>();
        _auditService = Substitute.For<IAuditService>();
        _handler = new DeleteRoleCommandHandler(_roleManager, _userManager, _permissionService, _auditService);
    }

    [Fact]
    public async Task Should_Delete_Role_Successfully()
    {
        var roleId = Guid.NewGuid();
        var role = new ApplicationRole { Id = roleId, Name = "Custom", IsSystemRole = false };
        var command = new DeleteRoleCommand(roleId) { CurrentUserId = Guid.NewGuid() };

        _roleManager.FindByIdAsync(roleId.ToString()).Returns(role);
        _userManager.GetUsersInRoleAsync("Custom").Returns(new List<ApplicationUser>());
        _roleManager.DeleteAsync(role).Returns(IdentityResult.Success);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        await _permissionService.Received(1).RemoveAllRolePermissionsAsync(roleId, Arg.Any<CancellationToken>());
        await _auditService.Received(1).LogAsync(
            AuditActions.RoleDeleted, "Role",
            roleId.ToString(), Arg.Any<string?>(), Arg.Any<Guid?>(),
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_Fail_When_Role_Not_Found()
    {
        var command = new DeleteRoleCommand(Guid.NewGuid()) { CurrentUserId = Guid.NewGuid() };
        _roleManager.FindByIdAsync(command.RoleId.ToString()).Returns((ApplicationRole?)null);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("Role not found.");
    }

    [Fact]
    public async Task Should_Fail_When_Deleting_System_Role()
    {
        var roleId = Guid.NewGuid();
        var role = new ApplicationRole { Id = roleId, Name = "Admin", IsSystemRole = true };
        var command = new DeleteRoleCommand(roleId) { CurrentUserId = Guid.NewGuid() };

        _roleManager.FindByIdAsync(roleId.ToString()).Returns(role);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("Cannot delete a system role.");
    }

    [Fact]
    public async Task Should_Fail_When_Role_Has_Users()
    {
        var roleId = Guid.NewGuid();
        var role = new ApplicationRole { Id = roleId, Name = "Custom", IsSystemRole = false };
        var command = new DeleteRoleCommand(roleId) { CurrentUserId = Guid.NewGuid() };

        _roleManager.FindByIdAsync(roleId.ToString()).Returns(role);
        _userManager.GetUsersInRoleAsync("Custom")
            .Returns(new List<ApplicationUser> { new() { Id = Guid.NewGuid(), Email = "u@test.com" } });

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("Cannot delete a role that has users assigned.");
    }

    [Fact]
    public async Task Should_Log_Audit_On_Delete()
    {
        var roleId = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();
        var role = new ApplicationRole { Id = roleId, Name = "Custom", IsSystemRole = false };
        var command = new DeleteRoleCommand(roleId) { CurrentUserId = currentUserId };

        _roleManager.FindByIdAsync(roleId.ToString()).Returns(role);
        _userManager.GetUsersInRoleAsync("Custom").Returns(new List<ApplicationUser>());
        _roleManager.DeleteAsync(role).Returns(IdentityResult.Success);

        await _handler.Handle(command, CancellationToken.None);

        await _auditService.Received(1).LogAsync(
            AuditActions.RoleDeleted, "Role",
            roleId.ToString(), Arg.Any<string?>(), currentUserId,
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }
}
