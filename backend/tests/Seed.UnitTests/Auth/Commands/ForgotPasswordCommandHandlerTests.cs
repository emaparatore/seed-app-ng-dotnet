using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using NSubstitute;
using Seed.Application.Auth.Commands.ForgotPassword;
using Seed.Application.Common.Interfaces;
using Seed.Domain.Entities;
using Seed.Shared.Configuration;

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
        var clientSettings = Options.Create(new ClientSettings { BaseUrl = "http://localhost:4200" });
        _handler = new ForgotPasswordCommandHandler(_userManager, _emailService, clientSettings);
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
    public async Task Should_Generate_Token_And_Send_ResetLink_When_User_Exists()
    {
        var command = new ForgotPasswordCommand("user@test.com");
        var user = new ApplicationUser { Email = command.Email, IsActive = true };
        _userManager.FindByEmailAsync(command.Email).Returns(user);
        _userManager.GeneratePasswordResetTokenAsync(user).Returns("reset-token-123");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        var expectedLink = $"http://localhost:4200/reset-password?email={WebUtility.UrlEncode(command.Email)}&token={WebUtility.UrlEncode("reset-token-123")}";
        await _emailService.Received(1)
            .SendPasswordResetEmailAsync(command.Email, expectedLink, Arg.Any<CancellationToken>());
    }
}
