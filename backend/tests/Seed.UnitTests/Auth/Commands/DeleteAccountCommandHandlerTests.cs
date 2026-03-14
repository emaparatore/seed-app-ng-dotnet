using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using NSubstitute;
using Seed.Application.Auth.Commands.DeleteAccount;
using Seed.Application.Common.Interfaces;
using Seed.Domain.Entities;

namespace Seed.UnitTests.Auth.Commands;

public class DeleteAccountCommandHandlerTests
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ITokenService _tokenService;
    private readonly DeleteAccountCommandHandler _handler;

    public DeleteAccountCommandHandlerTests()
    {
        var store = Substitute.For<IUserStore<ApplicationUser>>();
        _userManager = Substitute.For<UserManager<ApplicationUser>>(
            store, null, null, null, null, null, null, null, null);
        _tokenService = Substitute.For<ITokenService>();
        _handler = new DeleteAccountCommandHandler(_userManager, _tokenService);
    }

    [Fact]
    public async Task Should_Deactivate_User_And_Revoke_All_Tokens()
    {
        var userId = Guid.NewGuid();
        var user = new ApplicationUser
        {
            Id = userId,
            Email = "user@test.com",
            FirstName = "John",
            LastName = "Doe",
            IsActive = true
        };
        var command = new DeleteAccountCommand(userId, "Password1");

        _userManager.FindByIdAsync(userId.ToString()).Returns(user);
        _userManager.CheckPasswordAsync(user, command.Password).Returns(true);
        _userManager.UpdateAsync(user).Returns(IdentityResult.Success);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        user.IsActive.Should().BeFalse();
        await _userManager.Received(1).UpdateAsync(user);
        await _tokenService.Received(1).RevokeAllUserTokensAsync(userId);
    }

    [Fact]
    public async Task Should_Fail_When_User_Not_Found()
    {
        var command = new DeleteAccountCommand(Guid.NewGuid(), "Password1");
        _userManager.FindByIdAsync(command.UserId.ToString()).Returns((ApplicationUser?)null);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("Account not found.");
    }

    [Fact]
    public async Task Should_Fail_When_User_Already_Deactivated()
    {
        var userId = Guid.NewGuid();
        var user = new ApplicationUser
        {
            Id = userId,
            Email = "user@test.com",
            IsActive = false
        };
        var command = new DeleteAccountCommand(userId, "Password1");

        _userManager.FindByIdAsync(userId.ToString()).Returns(user);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("Account not found.");
    }

    [Fact]
    public async Task Should_Fail_When_Password_Is_Invalid()
    {
        var userId = Guid.NewGuid();
        var user = new ApplicationUser
        {
            Id = userId,
            Email = "user@test.com",
            IsActive = true
        };
        var command = new DeleteAccountCommand(userId, "WrongPassword");

        _userManager.FindByIdAsync(userId.ToString()).Returns(user);
        _userManager.CheckPasswordAsync(user, command.Password).Returns(false);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("Invalid password.");
    }
}
