using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using NSubstitute;
using Seed.Application.Auth.Commands.Login;
using Seed.Application.Common.Interfaces;
using Seed.Application.Common.Models;
using Seed.Domain.Entities;

namespace Seed.UnitTests.Auth.Commands;

public class LoginCommandHandlerTests
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ITokenService _tokenService;
    private readonly LoginCommandHandler _handler;

    public LoginCommandHandlerTests()
    {
        var store = Substitute.For<IUserStore<ApplicationUser>>();
        _userManager = Substitute.For<UserManager<ApplicationUser>>(
            store, null, null, null, null, null, null, null, null);
        _tokenService = Substitute.For<ITokenService>();
        _handler = new LoginCommandHandler(_userManager, _tokenService);
    }

    [Fact]
    public async Task Should_Fail_When_User_Not_Found()
    {
        var command = new LoginCommand("nobody@test.com", "Password1");
        _userManager.FindByEmailAsync(command.Email).Returns((ApplicationUser?)null);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("Invalid email or password.");
    }

    [Fact]
    public async Task Should_Fail_When_User_Is_Not_Active()
    {
        var command = new LoginCommand("inactive@test.com", "Password1");
        _userManager.FindByEmailAsync(command.Email)
            .Returns(new ApplicationUser { Email = command.Email, IsActive = false });

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("This account has been deactivated.");
    }

    [Fact]
    public async Task Should_Fail_When_Password_Is_Wrong()
    {
        var command = new LoginCommand("user@test.com", "WrongPass1");
        var user = new ApplicationUser { Email = command.Email, IsActive = true };
        _userManager.FindByEmailAsync(command.Email).Returns(user);
        _userManager.CheckPasswordAsync(user, command.Password).Returns(false);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("Invalid email or password.");
    }

    [Fact]
    public async Task Should_Return_AuthResponse_On_Successful_Login()
    {
        var command = new LoginCommand("user@test.com", "Password1");
        var userId = Guid.NewGuid();
        var user = new ApplicationUser
        {
            Id = userId,
            Email = command.Email,
            FirstName = "John",
            LastName = "Doe",
            IsActive = true
        };

        _userManager.FindByEmailAsync(command.Email).Returns(user);
        _userManager.CheckPasswordAsync(user, command.Password).Returns(true);

        var tokenResult = new TokenResult("access-token", "refresh-token", DateTime.UtcNow.AddMinutes(15), userId);
        _tokenService.GenerateTokensAsync(user).Returns(tokenResult);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.AccessToken.Should().Be("access-token");
        result.Data.User.Email.Should().Be(command.Email);
    }
}
