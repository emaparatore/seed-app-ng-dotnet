using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using NSubstitute;
using Seed.Application.Auth.Commands.Register;
using Seed.Application.Common.Interfaces;
using Seed.Application.Common.Models;
using Seed.Domain.Entities;

namespace Seed.UnitTests.Auth.Commands;

public class RegisterCommandHandlerTests
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ITokenService _tokenService;
    private readonly RegisterCommandHandler _handler;

    public RegisterCommandHandlerTests()
    {
        var store = Substitute.For<IUserStore<ApplicationUser>>();
        _userManager = Substitute.For<UserManager<ApplicationUser>>(
            store, null, null, null, null, null, null, null, null);
        _tokenService = Substitute.For<ITokenService>();
        _handler = new RegisterCommandHandler(_userManager, _tokenService);
    }

    [Fact]
    public async Task Should_Fail_When_Email_Already_Exists()
    {
        var command = new RegisterCommand("existing@test.com", "Password1", "John", "Doe");
        _userManager.FindByEmailAsync(command.Email)
            .Returns(new ApplicationUser { Email = command.Email });

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("A user with this email already exists.");
    }

    [Fact]
    public async Task Should_Fail_When_UserManager_CreateAsync_Fails()
    {
        var command = new RegisterCommand("new@test.com", "Password1", "John", "Doe");
        _userManager.FindByEmailAsync(command.Email).Returns((ApplicationUser?)null);
        _userManager.CreateAsync(Arg.Any<ApplicationUser>(), command.Password)
            .Returns(IdentityResult.Failed(new IdentityError { Description = "Password too weak" }));

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("Password too weak");
    }

    [Fact]
    public async Task Should_Return_AuthResponse_On_Successful_Registration()
    {
        var command = new RegisterCommand("new@test.com", "Password1", "John", "Doe");
        var userId = Guid.NewGuid();

        _userManager.FindByEmailAsync(command.Email).Returns((ApplicationUser?)null);
        _userManager.CreateAsync(Arg.Any<ApplicationUser>(), command.Password)
            .Returns(IdentityResult.Success);

        var tokenResult = new TokenResult("access-token", "refresh-token", DateTime.UtcNow.AddMinutes(15), userId);
        _tokenService.GenerateTokensAsync(Arg.Any<ApplicationUser>()).Returns(tokenResult);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.AccessToken.Should().Be("access-token");
        result.Data.RefreshToken.Should().Be("refresh-token");
        result.Data.User.Email.Should().Be(command.Email);
        result.Data.User.FirstName.Should().Be(command.FirstName);
    }
}
