using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using NSubstitute;
using Seed.Application.Admin.Users.Commands.ToggleUserStatus;
using Seed.Application.Common.Interfaces;
using Seed.Domain.Authorization;
using Seed.Domain.Entities;

namespace Seed.UnitTests.Admin.Users;

public class ToggleUserStatusCommandHandlerTests
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ITokenBlacklistService _tokenBlacklistService;
    private readonly IAuditService _auditService;
    private readonly ToggleUserStatusCommandHandler _handler;

    public ToggleUserStatusCommandHandlerTests()
    {
        var store = Substitute.For<IUserStore<ApplicationUser>>();
        _userManager = Substitute.For<UserManager<ApplicationUser>>(
            store, null, null, null, null, null, null, null, null);
        _tokenBlacklistService = Substitute.For<ITokenBlacklistService>();
        _auditService = Substitute.For<IAuditService>();
        _handler = new ToggleUserStatusCommandHandler(_userManager, _tokenBlacklistService, _auditService);
    }

    [Fact]
    public async Task Should_Toggle_Status_Successfully()
    {
        var userId = Guid.NewGuid();
        var user = new ApplicationUser { Id = userId, Email = "user@test.com", IsActive = true };
        var command = new ToggleUserStatusCommand(false) { UserId = userId, CurrentUserId = Guid.NewGuid() };

        _userManager.FindByIdAsync(userId.ToString()).Returns(user);
        _userManager.GetRolesAsync(user).Returns(new List<string> { "User" });
        _userManager.UpdateAsync(user).Returns(IdentityResult.Success);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        user.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Should_Blacklist_Tokens_When_Deactivating()
    {
        var userId = Guid.NewGuid();
        var user = new ApplicationUser { Id = userId, Email = "user@test.com", IsActive = true };
        var command = new ToggleUserStatusCommand(false) { UserId = userId, CurrentUserId = Guid.NewGuid() };

        _userManager.FindByIdAsync(userId.ToString()).Returns(user);
        _userManager.GetRolesAsync(user).Returns(new List<string>());
        _userManager.UpdateAsync(user).Returns(IdentityResult.Success);

        await _handler.Handle(command, CancellationToken.None);

        await _tokenBlacklistService.Received(1).BlacklistUserTokensAsync(userId);
    }

    [Fact]
    public async Task Should_Not_Blacklist_Tokens_When_Activating()
    {
        var userId = Guid.NewGuid();
        var user = new ApplicationUser { Id = userId, Email = "user@test.com", IsActive = false };
        var command = new ToggleUserStatusCommand(true) { UserId = userId, CurrentUserId = Guid.NewGuid() };

        _userManager.FindByIdAsync(userId.ToString()).Returns(user);
        _userManager.GetRolesAsync(user).Returns(new List<string>());
        _userManager.UpdateAsync(user).Returns(IdentityResult.Success);

        await _handler.Handle(command, CancellationToken.None);

        await _tokenBlacklistService.DidNotReceive().BlacklistUserTokensAsync(Arg.Any<Guid>());
    }

    [Fact]
    public async Task Should_Fail_When_Changing_Own_Status()
    {
        var userId = Guid.NewGuid();
        var command = new ToggleUserStatusCommand(false) { UserId = userId, CurrentUserId = userId };

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("You cannot change your own status.");
    }

    [Fact]
    public async Task Should_Fail_When_Toggling_SuperAdmin()
    {
        var userId = Guid.NewGuid();
        var user = new ApplicationUser { Id = userId, Email = "admin@test.com" };
        var command = new ToggleUserStatusCommand(false) { UserId = userId, CurrentUserId = Guid.NewGuid() };

        _userManager.FindByIdAsync(userId.ToString()).Returns(user);
        _userManager.GetRolesAsync(user).Returns(new List<string> { SystemRoles.SuperAdmin });

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("Cannot change the status of a SuperAdmin user.");
    }

    [Fact]
    public async Task Should_Log_Audit_On_Status_Change()
    {
        var userId = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();
        var user = new ApplicationUser { Id = userId, Email = "user@test.com", IsActive = true };
        var command = new ToggleUserStatusCommand(false) { UserId = userId, CurrentUserId = currentUserId };

        _userManager.FindByIdAsync(userId.ToString()).Returns(user);
        _userManager.GetRolesAsync(user).Returns(new List<string>());
        _userManager.UpdateAsync(user).Returns(IdentityResult.Success);

        await _handler.Handle(command, CancellationToken.None);

        await _auditService.Received(1).LogAsync(
            AuditActions.UserStatusChanged, "User",
            userId.ToString(), Arg.Any<string?>(), currentUserId,
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }
}
