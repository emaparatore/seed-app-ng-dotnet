using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using NSubstitute;
using Seed.Application.Auth.Commands.ChangePassword;
using Seed.Domain.Entities;

namespace Seed.UnitTests.Auth.Commands;

public class ChangePasswordCommandHandlerTests
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ChangePasswordCommandHandler _handler;

    public ChangePasswordCommandHandlerTests()
    {
        var store = Substitute.For<IUserStore<ApplicationUser>>();
        _userManager = Substitute.For<UserManager<ApplicationUser>>(
            store, null, null, null, null, null, null, null, null);
        _handler = new ChangePasswordCommandHandler(_userManager);
    }

    [Fact]
    public async Task Should_Change_Password_And_Clear_MustChangePassword_Flag()
    {
        var userId = Guid.NewGuid();
        var user = new ApplicationUser
        {
            Id = userId,
            Email = "user@test.com",
            IsActive = true,
            MustChangePassword = true
        };
        var command = new ChangePasswordCommand(userId.ToString(), "OldPassword1", "NewPassword1");

        _userManager.FindByIdAsync(userId.ToString()).Returns(user);
        _userManager.CheckPasswordAsync(user, "OldPassword1").Returns(true);
        _userManager.ChangePasswordAsync(user, "OldPassword1", "NewPassword1").Returns(IdentityResult.Success);
        _userManager.UpdateAsync(user).Returns(IdentityResult.Success);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        user.MustChangePassword.Should().BeFalse();
        await _userManager.Received(1).ChangePasswordAsync(user, "OldPassword1", "NewPassword1");
        await _userManager.Received(1).UpdateAsync(user);
    }

    [Fact]
    public async Task Should_Fail_When_User_Not_Found()
    {
        var command = new ChangePasswordCommand(Guid.NewGuid().ToString(), "OldPassword1", "NewPassword1");
        _userManager.FindByIdAsync(command.UserId).Returns((ApplicationUser?)null);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("User not found.");
    }

    [Fact]
    public async Task Should_Fail_When_User_Is_Inactive()
    {
        var userId = Guid.NewGuid();
        var user = new ApplicationUser
        {
            Id = userId,
            Email = "user@test.com",
            IsActive = false
        };
        var command = new ChangePasswordCommand(userId.ToString(), "OldPassword1", "NewPassword1");

        _userManager.FindByIdAsync(userId.ToString()).Returns(user);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("User not found.");
    }

    [Fact]
    public async Task Should_Fail_When_Current_Password_Is_Incorrect()
    {
        var userId = Guid.NewGuid();
        var user = new ApplicationUser
        {
            Id = userId,
            Email = "user@test.com",
            IsActive = true
        };
        var command = new ChangePasswordCommand(userId.ToString(), "WrongPassword", "NewPassword1");

        _userManager.FindByIdAsync(userId.ToString()).Returns(user);
        _userManager.CheckPasswordAsync(user, "WrongPassword").Returns(false);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("Current password is incorrect.");
    }

    [Fact]
    public async Task Should_Fail_When_ChangePassword_Returns_Errors()
    {
        var userId = Guid.NewGuid();
        var user = new ApplicationUser
        {
            Id = userId,
            Email = "user@test.com",
            IsActive = true
        };
        var command = new ChangePasswordCommand(userId.ToString(), "OldPassword1", "weak");

        _userManager.FindByIdAsync(userId.ToString()).Returns(user);
        _userManager.CheckPasswordAsync(user, "OldPassword1").Returns(true);
        _userManager.ChangePasswordAsync(user, "OldPassword1", "weak")
            .Returns(IdentityResult.Failed(new IdentityError { Description = "Password too short." }));

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("Password too short.");
    }
}
