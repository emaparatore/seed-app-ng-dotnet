using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using NSubstitute;
using Seed.Application.Auth.Commands.ConfirmEmail;
using Seed.Application.Common.Interfaces;
using Seed.Application.Common.Models;
using Seed.Domain.Entities;

namespace Seed.UnitTests.Auth.Commands;

public class ConfirmEmailCommandHandlerTests
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ITokenService _tokenService;
    private readonly ConfirmEmailCommandHandler _handler;

    public ConfirmEmailCommandHandlerTests()
    {
        var store = Substitute.For<IUserStore<ApplicationUser>>();
        _userManager = Substitute.For<UserManager<ApplicationUser>>(
            store, null, null, null, null, null, null, null, null);
        _tokenService = Substitute.For<ITokenService>();
        _handler = new ConfirmEmailCommandHandler(_userManager, _tokenService);
    }

    [Fact]
    public async Task Should_Fail_When_User_Not_Found()
    {
        var command = new ConfirmEmailCommand("nobody@test.com", "some-token");
        _userManager.FindByEmailAsync(command.Email).Returns((ApplicationUser?)null);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("Invalid or expired verification link.");
    }

    [Fact]
    public async Task Should_Fail_When_User_Is_Inactive()
    {
        var command = new ConfirmEmailCommand("inactive@test.com", "some-token");
        _userManager.FindByEmailAsync(command.Email)
            .Returns(new ApplicationUser { Email = command.Email, IsActive = false });

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("Invalid or expired verification link.");
    }

    [Fact]
    public async Task Should_Fail_When_Email_Already_Confirmed()
    {
        var command = new ConfirmEmailCommand("verified@test.com", "some-token");
        _userManager.FindByEmailAsync(command.Email)
            .Returns(new ApplicationUser { Email = command.Email, IsActive = true, EmailConfirmed = true });

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("Email address has already been verified.");
    }

    [Fact]
    public async Task Should_Fail_When_Token_Is_Invalid()
    {
        var command = new ConfirmEmailCommand("user@test.com", "bad-token");
        var user = new ApplicationUser { Email = command.Email, IsActive = true, EmailConfirmed = false };
        _userManager.FindByEmailAsync(command.Email).Returns(user);
        _userManager.ConfirmEmailAsync(user, command.Token)
            .Returns(IdentityResult.Failed(new IdentityError { Description = "Invalid token" }));

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("Invalid or expired verification link.");
    }

    [Fact]
    public async Task Should_Return_AuthResponse_When_Token_Is_Valid()
    {
        var userId = Guid.NewGuid();
        var command = new ConfirmEmailCommand("user@test.com", "valid-token");
        var user = new ApplicationUser
        {
            Id = userId,
            Email = command.Email,
            FirstName = "Jane",
            LastName = "Doe",
            IsActive = true,
            EmailConfirmed = false
        };
        _userManager.FindByEmailAsync(command.Email).Returns(user);
        _userManager.ConfirmEmailAsync(user, command.Token).Returns(IdentityResult.Success);

        var tokenResult = new TokenResult("access-token", "refresh-token", DateTime.UtcNow.AddMinutes(15), userId);
        _tokenService.GenerateTokensAsync(user).Returns(tokenResult);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.AccessToken.Should().Be("access-token");
        result.Data.User.Email.Should().Be(command.Email);
    }
}
