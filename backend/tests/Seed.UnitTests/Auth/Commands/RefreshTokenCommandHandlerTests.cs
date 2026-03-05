using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using NSubstitute;
using Seed.Application.Auth.Commands.RefreshToken;
using Seed.Application.Common.Interfaces;
using Seed.Application.Common.Models;
using Seed.Domain.Entities;

namespace Seed.UnitTests.Auth.Commands;

public class RefreshTokenCommandHandlerTests
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ITokenService _tokenService;
    private readonly RefreshTokenCommandHandler _handler;

    public RefreshTokenCommandHandlerTests()
    {
        var store = Substitute.For<IUserStore<ApplicationUser>>();
        _userManager = Substitute.For<UserManager<ApplicationUser>>(
            store, null, null, null, null, null, null, null, null);
        _tokenService = Substitute.For<ITokenService>();
        _handler = new RefreshTokenCommandHandler(_tokenService, _userManager);
    }

    [Fact]
    public async Task Should_Fail_When_Token_Is_Invalid()
    {
        var command = new RefreshTokenCommand("invalid-token");
        _tokenService.RefreshTokenAsync(command.RefreshToken).Returns((TokenResult?)null);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("Invalid or expired refresh token.");
    }

    [Fact]
    public async Task Should_Fail_When_User_Not_Found()
    {
        var command = new RefreshTokenCommand("valid-token");
        var userId = Guid.NewGuid();
        var tokenResult = new TokenResult("new-access", "new-refresh", DateTime.UtcNow.AddMinutes(15), userId);

        _tokenService.RefreshTokenAsync(command.RefreshToken).Returns(tokenResult);
        _userManager.FindByIdAsync(userId.ToString()).Returns((ApplicationUser?)null);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("Invalid or expired refresh token.");
    }

    [Fact]
    public async Task Should_Fail_When_User_Is_Not_Active()
    {
        var command = new RefreshTokenCommand("valid-token");
        var userId = Guid.NewGuid();
        var tokenResult = new TokenResult("new-access", "new-refresh", DateTime.UtcNow.AddMinutes(15), userId);

        _tokenService.RefreshTokenAsync(command.RefreshToken).Returns(tokenResult);
        _userManager.FindByIdAsync(userId.ToString())
            .Returns(new ApplicationUser { Id = userId, IsActive = false });

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("Invalid or expired refresh token.");
    }

    [Fact]
    public async Task Should_Return_New_Tokens_On_Successful_Refresh()
    {
        var command = new RefreshTokenCommand("valid-token");
        var userId = Guid.NewGuid();
        var tokenResult = new TokenResult("new-access", "new-refresh", DateTime.UtcNow.AddMinutes(15), userId);
        var user = new ApplicationUser
        {
            Id = userId,
            Email = "user@test.com",
            FirstName = "John",
            LastName = "Doe",
            IsActive = true
        };

        _tokenService.RefreshTokenAsync(command.RefreshToken).Returns(tokenResult);
        _userManager.FindByIdAsync(userId.ToString()).Returns(user);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.Data!.AccessToken.Should().Be("new-access");
        result.Data.RefreshToken.Should().Be("new-refresh");
        result.Data.User.Email.Should().Be("user@test.com");
    }
}
