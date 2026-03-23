using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using NSubstitute;
using Seed.Application.Admin.Roles.Commands.CreateRole;
using Seed.Application.Common.Interfaces;
using Seed.Domain.Authorization;
using Seed.Domain.Entities;

namespace Seed.UnitTests.Admin.Roles;

public class CreateRoleCommandHandlerTests
{
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly IPermissionService _permissionService;
    private readonly IAuditService _auditService;
    private readonly CreateRoleCommandHandler _handler;

    public CreateRoleCommandHandlerTests()
    {
        var roleStore = Substitute.For<IRoleStore<ApplicationRole>>();
        _roleManager = Substitute.For<RoleManager<ApplicationRole>>(
            roleStore, null, null, null, null);
        _permissionService = Substitute.For<IPermissionService>();
        _auditService = Substitute.For<IAuditService>();
        _handler = new CreateRoleCommandHandler(_roleManager, _permissionService, _auditService);
    }

    [Fact]
    public async Task Should_Create_Role_Successfully()
    {
        var command = new CreateRoleCommand("Editor", "Can edit content", ["Users.Read", "Users.Update"]);
        _roleManager.RoleExistsAsync("Editor").Returns(false);
        _roleManager.CreateAsync(Arg.Any<ApplicationRole>()).Returns(IdentityResult.Success);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.Data.Should().NotBeEmpty();
        await _permissionService.Received(1).SetRolePermissionsAsync(
            Arg.Any<Guid>(), Arg.Is<IEnumerable<string>>(p => p.Count() == 2), Arg.Any<CancellationToken>());
        await _auditService.Received(1).LogAsync(
            AuditActions.RoleCreated, "Role",
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<Guid?>(),
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_Fail_When_Name_Already_Exists()
    {
        var command = new CreateRoleCommand("Admin", null, []);
        _roleManager.RoleExistsAsync("Admin").Returns(true);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("A role with this name already exists.");
    }

    [Fact]
    public async Task Should_Set_IsSystemRole_To_False()
    {
        var command = new CreateRoleCommand("Custom", null, []);
        _roleManager.RoleExistsAsync("Custom").Returns(false);
        _roleManager.CreateAsync(Arg.Any<ApplicationRole>())
            .Returns(callInfo =>
            {
                var role = callInfo.ArgAt<ApplicationRole>(0);
                role.IsSystemRole.Should().BeFalse();
                return IdentityResult.Success;
            });

        await _handler.Handle(command, CancellationToken.None);
    }

    [Fact]
    public async Task Should_Not_Set_Permissions_When_Empty()
    {
        var command = new CreateRoleCommand("Empty", null, []);
        _roleManager.RoleExistsAsync("Empty").Returns(false);
        _roleManager.CreateAsync(Arg.Any<ApplicationRole>()).Returns(IdentityResult.Success);

        await _handler.Handle(command, CancellationToken.None);

        await _permissionService.DidNotReceive().SetRolePermissionsAsync(
            Arg.Any<Guid>(), Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_Fail_When_RoleManager_Create_Fails()
    {
        var command = new CreateRoleCommand("Bad", null, []);
        _roleManager.RoleExistsAsync("Bad").Returns(false);
        _roleManager.CreateAsync(Arg.Any<ApplicationRole>())
            .Returns(IdentityResult.Failed(new IdentityError { Description = "Create failed" }));

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("Create failed");
    }
}
