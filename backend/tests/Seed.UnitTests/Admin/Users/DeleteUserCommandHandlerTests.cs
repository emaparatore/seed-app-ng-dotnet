using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using NSubstitute;
using Seed.Application.Admin.Users.Commands.DeleteUser;
using Seed.Application.Common.Interfaces;
using Seed.Domain.Authorization;
using Seed.Domain.Entities;

namespace Seed.UnitTests.Admin.Users;

public class DeleteUserCommandHandlerTests
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ITokenBlacklistService _tokenBlacklistService;
    private readonly IAuditService _auditService;
    private readonly DeleteUserCommandHandler _handler;

    public DeleteUserCommandHandlerTests()
    {
        var store = Substitute.For<IUserStore<ApplicationUser>>();
        _userManager = Substitute.For<UserManager<ApplicationUser>>(
            store, null, null, null, null, null, null, null, null);
        _tokenBlacklistService = Substitute.For<ITokenBlacklistService>();
        _auditService = Substitute.For<IAuditService>();
        _handler = new DeleteUserCommandHandler(_userManager, _tokenBlacklistService, _auditService);
    }

    [Fact]
    public async Task Should_Soft_Delete_User_Successfully()
    {
        var userId = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();
        var user = new ApplicationUser { Id = userId, Email = "user@test.com", IsActive = true };
        var command = new DeleteUserCommand(userId) { CurrentUserId = currentUserId };

        _userManager.FindByIdAsync(userId.ToString()).Returns(user);
        _userManager.GetRolesAsync(user).Returns(new List<string> { "User" });
        _userManager.UpdateAsync(user).Returns(IdentityResult.Success);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        user.IsDeleted.Should().BeTrue();
        user.DeletedAt.Should().NotBeNull();
        user.IsActive.Should().BeFalse();
        await _tokenBlacklistService.Received(1).BlacklistUserTokensAsync(userId);
    }

    [Fact]
    public async Task Should_Fail_When_Deleting_Self()
    {
        var userId = Guid.NewGuid();
        var command = new DeleteUserCommand(userId) { CurrentUserId = userId };

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("You cannot delete your own account.");
    }

    [Fact]
    public async Task Should_Fail_When_Deleting_SuperAdmin()
    {
        var userId = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();
        var user = new ApplicationUser { Id = userId, Email = "admin@test.com" };
        var command = new DeleteUserCommand(userId) { CurrentUserId = currentUserId };

        _userManager.FindByIdAsync(userId.ToString()).Returns(user);
        _userManager.GetRolesAsync(user).Returns(new List<string> { SystemRoles.SuperAdmin });

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("Cannot delete a SuperAdmin user.");
    }

    [Fact]
    public async Task Should_Fail_When_User_Not_Found()
    {
        var command = new DeleteUserCommand(Guid.NewGuid()) { CurrentUserId = Guid.NewGuid() };
        _userManager.FindByIdAsync(command.UserId.ToString()).Returns((ApplicationUser?)null);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("User not found.");
    }

    [Fact]
    public async Task Should_Log_Audit_On_Delete()
    {
        var userId = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();
        var user = new ApplicationUser { Id = userId, Email = "user@test.com" };
        var command = new DeleteUserCommand(userId) { CurrentUserId = currentUserId };

        _userManager.FindByIdAsync(userId.ToString()).Returns(user);
        _userManager.GetRolesAsync(user).Returns(new List<string>());
        _userManager.UpdateAsync(user).Returns(IdentityResult.Success);

        await _handler.Handle(command, CancellationToken.None);

        await _auditService.Received(1).LogAsync(
            AuditActions.UserDeleted, "User",
            userId.ToString(), Arg.Any<string?>(), currentUserId,
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }
}
