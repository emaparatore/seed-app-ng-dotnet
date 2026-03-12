using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using NSubstitute;
using Seed.Application.Auth.Commands.ForgotPassword;
using Seed.Application.Common.Interfaces;
using Seed.Domain.Entities;

namespace Seed.UnitTests.Auth.Commands;

public class ForgotPasswordCommandHandlerTests
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IEmailService _emailService;
    private readonly ForgotPasswordCommandHandler _handler;

    public ForgotPasswordCommandHandlerTests()
    {
        var store = Substitute.For<IUserStore<ApplicationUser>>();
        _userManager = Substitute.For<UserManager<ApplicationUser>>(
            store, null, null, null, null, null, null, null, null);
        _emailService = Substitute.For<IEmailService>();
        _handler = new ForgotPasswordCommandHandler(_userManager, _emailService);
    }

    [Fact]
    public async Task Should_Return_Success_When_User_Not_Found()
    {
        var command = new ForgotPasswordCommand("nobody@test.com");
        _userManager.FindByEmailAsync(command.Email).Returns((ApplicationUser?)null);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        await _emailService.DidNotReceiveWithAnyArgs()
            .SendPasswordResetEmailAsync(default!, default!, default);
    }

    [Fact]
    public async Task Should_Return_Success_When_User_Is_Inactive()
    {
        var command = new ForgotPasswordCommand("inactive@test.com");
        _userManager.FindByEmailAsync(command.Email)
            .Returns(new ApplicationUser { Email = command.Email, IsActive = false });

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        await _emailService.DidNotReceiveWithAnyArgs()
            .SendPasswordResetEmailAsync(default!, default!, default);
    }

    [Fact]
    public async Task Should_Generate_Token_And_Send_Email_When_User_Exists()
    {
        var command = new ForgotPasswordCommand("user@test.com");
        var user = new ApplicationUser { Email = command.Email, IsActive = true };
        _userManager.FindByEmailAsync(command.Email).Returns(user);
        _userManager.GeneratePasswordResetTokenAsync(user).Returns("reset-token-123");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        await _emailService.Received(1)
            .SendPasswordResetEmailAsync(command.Email, "reset-token-123", Arg.Any<CancellationToken>());
    }
}
