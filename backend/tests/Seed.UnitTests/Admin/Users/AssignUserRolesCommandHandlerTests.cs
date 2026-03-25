using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using NSubstitute;
using Seed.Application.Admin.Users.Commands.AssignUserRoles;
using Seed.Application.Common.Interfaces;
using Seed.Domain.Authorization;
using Seed.Domain.Entities;

namespace Seed.UnitTests.Admin.Users;

public class AssignUserRolesCommandHandlerTests
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly IPermissionService _permissionService;
    private readonly ITokenBlacklistService _tokenBlacklistService;
    private readonly IAuditService _auditService;
    private readonly AssignUserRolesCommandHandler _handler;

    public AssignUserRolesCommandHandlerTests()
    {
        var userStore = Substitute.For<IUserStore<ApplicationUser>>();
        _userManager = Substitute.For<UserManager<ApplicationUser>>(
            userStore, null, null, null, null, null, null, null, null);

        var roleStore = Substitute.For<IRoleStore<ApplicationRole>>();
        _roleManager = Substitute.For<RoleManager<ApplicationRole>>(
            roleStore, null, null, null, null);

        _permissionService = Substitute.For<IPermissionService>();
        _tokenBlacklistService = Substitute.For<ITokenBlacklistService>();
        _auditService = Substitute.For<IAuditService>();
        _handler = new AssignUserRolesCommandHandler(
            _userManager, _roleManager, _permissionService, _tokenBlacklistService, _auditService);
    }

    [Fact]
    public async Task Should_Assign_Roles_Successfully()
    {
        var userId = Guid.NewGuid();
        var user = new ApplicationUser { Id = userId, Email = "user@test.com" };
        var command = new AssignUserRolesCommand(["Admin"]) { UserId = userId, CurrentUserId = Guid.NewGuid() };

        _userManager.FindByIdAsync(userId.ToString()).Returns(user);
        _userManager.GetRolesAsync(user).Returns(new List<string> { "User" });
        _roleManager.RoleExistsAsync("Admin").Returns(true);
        _userManager.RemoveFromRolesAsync(user, Arg.Any<IEnumerable<string>>()).Returns(IdentityResult.Success);
        _userManager.AddToRolesAsync(user, Arg.Any<IEnumerable<string>>()).Returns(IdentityResult.Success);
        _userManager.UpdateAsync(user).Returns(IdentityResult.Success);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        await _permissionService.Received(1).InvalidateUserPermissionsCacheAsync(userId);
        await _tokenBlacklistService.Received(1).BlacklistUserTokensAsync(userId);
    }

    [Fact]
    public async Task Should_Fail_When_Modifying_SuperAdmin_Roles()
    {
        var userId = Guid.NewGuid();
        var user = new ApplicationUser { Id = userId, Email = "admin@test.com" };
        var command = new AssignUserRolesCommand(["Admin"]) { UserId = userId, CurrentUserId = Guid.NewGuid() };

        _userManager.FindByIdAsync(userId.ToString()).Returns(user);
        _userManager.GetRolesAsync(user).Returns(new List<string> { SystemRoles.SuperAdmin });

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("Cannot modify the roles of a SuperAdmin user.");
    }

    [Fact]
    public async Task Should_Fail_When_Assigning_SuperAdmin_Role()
    {
        var userId = Guid.NewGuid();
        var user = new ApplicationUser { Id = userId, Email = "user@test.com" };
        var command = new AssignUserRolesCommand([SystemRoles.SuperAdmin])
        {
            UserId = userId, CurrentUserId = Guid.NewGuid()
        };

        _userManager.FindByIdAsync(userId.ToString()).Returns(user);
        _userManager.GetRolesAsync(user).Returns(new List<string>());

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("Cannot assign the SuperAdmin role.");
    }

    [Fact]
    public async Task Should_Fail_When_Role_Does_Not_Exist()
    {
        var userId = Guid.NewGuid();
        var user = new ApplicationUser { Id = userId, Email = "user@test.com" };
        var command = new AssignUserRolesCommand(["NonExistent"])
        {
            UserId = userId, CurrentUserId = Guid.NewGuid()
        };

        _userManager.FindByIdAsync(userId.ToString()).Returns(user);
        _userManager.GetRolesAsync(user).Returns(new List<string>());
        _roleManager.RoleExistsAsync("NonExistent").Returns(false);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("Role 'NonExistent' does not exist.");
    }

    [Fact]
    public async Task Should_Invalidate_Cache_And_Blacklist_Tokens()
    {
        var userId = Guid.NewGuid();
        var user = new ApplicationUser { Id = userId, Email = "user@test.com" };
        var command = new AssignUserRolesCommand(["Admin"]) { UserId = userId, CurrentUserId = Guid.NewGuid() };

        _userManager.FindByIdAsync(userId.ToString()).Returns(user);
        _userManager.GetRolesAsync(user).Returns(new List<string>());
        _roleManager.RoleExistsAsync("Admin").Returns(true);
        _userManager.RemoveFromRolesAsync(user, Arg.Any<IEnumerable<string>>()).Returns(IdentityResult.Success);
        _userManager.AddToRolesAsync(user, Arg.Any<IEnumerable<string>>()).Returns(IdentityResult.Success);
        _userManager.UpdateAsync(user).Returns(IdentityResult.Success);

        await _handler.Handle(command, CancellationToken.None);

        await _permissionService.Received(1).InvalidateUserPermissionsCacheAsync(userId);
        await _tokenBlacklistService.Received(1).BlacklistUserTokensAsync(userId);
    }

    [Fact]
    public async Task Should_Log_Audit_With_Before_And_After_Roles()
    {
        var userId = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();
        var user = new ApplicationUser { Id = userId, Email = "user@test.com" };
        var command = new AssignUserRolesCommand(["Admin"]) { UserId = userId, CurrentUserId = currentUserId };

        _userManager.FindByIdAsync(userId.ToString()).Returns(user);
        _userManager.GetRolesAsync(user).Returns(new List<string> { "User" });
        _roleManager.RoleExistsAsync("Admin").Returns(true);
        _userManager.RemoveFromRolesAsync(user, Arg.Any<IEnumerable<string>>()).Returns(IdentityResult.Success);
        _userManager.AddToRolesAsync(user, Arg.Any<IEnumerable<string>>()).Returns(IdentityResult.Success);
        _userManager.UpdateAsync(user).Returns(IdentityResult.Success);

        await _handler.Handle(command, CancellationToken.None);

        await _auditService.Received(1).LogAsync(
            AuditActions.UserRolesChanged, "User",
            userId.ToString(), Arg.Any<string?>(), currentUserId,
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }
}
