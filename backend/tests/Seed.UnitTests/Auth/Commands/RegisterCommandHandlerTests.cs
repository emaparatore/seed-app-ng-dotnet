using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using NSubstitute;
using Seed.Application.Auth.Commands.Register;
using Seed.Application.Common.Interfaces;
using Seed.Domain.Entities;
using Seed.Shared.Configuration;

namespace Seed.UnitTests.Auth.Commands;

public class RegisterCommandHandlerTests
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IEmailService _emailService;
    private readonly RegisterCommandHandler _handler;

    public RegisterCommandHandlerTests()
    {
        var store = Substitute.For<IUserStore<ApplicationUser>>();
        _userManager = Substitute.For<UserManager<ApplicationUser>>(
            store, null, null, null, null, null, null, null, null);
        _emailService = Substitute.For<IEmailService>();
        var clientSettings = Options.Create(new ClientSettings { BaseUrl = "http://localhost:4200" });
        _handler = new RegisterCommandHandler(_userManager, _emailService, clientSettings);
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
    public async Task Should_Send_Verification_Email_And_Return_Message_On_Successful_Registration()
    {
        var command = new RegisterCommand("new@test.com", "Password1", "John", "Doe");

        _userManager.FindByEmailAsync(command.Email).Returns((ApplicationUser?)null);
        _userManager.CreateAsync(Arg.Any<ApplicationUser>(), command.Password)
            .Returns(IdentityResult.Success);
        _userManager.GenerateEmailConfirmationTokenAsync(Arg.Any<ApplicationUser>())
            .Returns("confirm-token-123");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.Data.Should().NotBeNullOrEmpty();
        await _emailService.Received(1)
            .SendEmailVerificationAsync(
                command.Email,
                Arg.Is<string>(link => link.Contains("confirm-email") && link.Contains("new%40test.com")),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_Not_Issue_Tokens_On_Registration()
    {
        var command = new RegisterCommand("new@test.com", "Password1", "John", "Doe");

        _userManager.FindByEmailAsync(command.Email).Returns((ApplicationUser?)null);
        _userManager.CreateAsync(Arg.Any<ApplicationUser>(), command.Password)
            .Returns(IdentityResult.Success);
        _userManager.GenerateEmailConfirmationTokenAsync(Arg.Any<ApplicationUser>())
            .Returns("confirm-token-123");

        var result = await _handler.Handle(command, CancellationToken.None);

        // Result is string message, not AuthResponse with tokens
        result.Succeeded.Should().BeTrue();
        result.Data.Should().BeOfType<string>();
    }
}
