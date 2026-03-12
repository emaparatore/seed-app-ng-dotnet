using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using NSubstitute;
using Seed.Application.Auth.Commands.ResetPassword;
using Seed.Domain.Entities;

namespace Seed.UnitTests.Auth.Commands;

public class ResetPasswordCommandHandlerTests
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ResetPasswordCommandHandler _handler;

    public ResetPasswordCommandHandlerTests()
    {
        var store = Substitute.For<IUserStore<ApplicationUser>>();
        _userManager = Substitute.For<UserManager<ApplicationUser>>(
            store, null, null, null, null, null, null, null, null);
        _handler = new ResetPasswordCommandHandler(_userManager);
    }

    [Fact]
    public async Task Should_Fail_When_User_Not_Found()
    {
        var command = new ResetPasswordCommand("nobody@test.com", "token", "NewPassword1");
        _userManager.FindByEmailAsync(command.Email).Returns((ApplicationUser?)null);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("Invalid or expired reset token.");
    }

    [Fact]
    public async Task Should_Fail_When_User_Is_Inactive()
    {
        var command = new ResetPasswordCommand("inactive@test.com", "token", "NewPassword1");
        _userManager.FindByEmailAsync(command.Email)
            .Returns(new ApplicationUser { Email = command.Email, IsActive = false });

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("Invalid or expired reset token.");
    }

    [Fact]
    public async Task Should_Fail_When_Token_Is_Invalid()
    {
        var command = new ResetPasswordCommand("user@test.com", "bad-token", "NewPassword1");
        var user = new ApplicationUser { Email = command.Email, IsActive = true };
        _userManager.FindByEmailAsync(command.Email).Returns(user);
        _userManager.ResetPasswordAsync(user, command.Token, command.NewPassword)
            .Returns(IdentityResult.Failed(new IdentityError { Description = "Invalid token." }));

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("Invalid token.");
    }

    [Fact]
    public async Task Should_Succeed_When_Token_Is_Valid()
    {
        var command = new ResetPasswordCommand("user@test.com", "valid-token", "NewPassword1");
        var user = new ApplicationUser { Email = command.Email, IsActive = true };
        _userManager.FindByEmailAsync(command.Email).Returns(user);
        _userManager.ResetPasswordAsync(user, command.Token, command.NewPassword)
            .Returns(IdentityResult.Success);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.Data.Should().Be("Password has been reset successfully.");
    }
}
