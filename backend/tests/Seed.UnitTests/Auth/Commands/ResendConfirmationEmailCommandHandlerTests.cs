using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using NSubstitute;
using Seed.Application.Auth.Commands.ResendConfirmationEmail;
using Seed.Application.Common.Interfaces;
using Seed.Domain.Entities;
using Seed.Shared.Configuration;

namespace Seed.UnitTests.Auth.Commands;

public class ResendConfirmationEmailCommandHandlerTests
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IEmailService _emailService;
    private readonly ResendConfirmationEmailCommandHandler _handler;

    public ResendConfirmationEmailCommandHandlerTests()
    {
        var store = Substitute.For<IUserStore<ApplicationUser>>();
        _userManager = Substitute.For<UserManager<ApplicationUser>>(
            store, null, null, null, null, null, null, null, null);
        _emailService = Substitute.For<IEmailService>();
        var clientSettings = Options.Create(new ClientSettings { BaseUrl = "http://localhost:4200" });
        var auditService = Substitute.For<IAuditService>();
        _handler = new ResendConfirmationEmailCommandHandler(_userManager, _emailService, clientSettings, auditService);
    }

    [Fact]
    public async Task Should_Return_Success_When_User_Not_Found()
    {
        var command = new ResendConfirmationEmailCommand("nobody@test.com");
        _userManager.FindByEmailAsync(command.Email).Returns((ApplicationUser?)null);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        await _emailService.DidNotReceiveWithAnyArgs()
            .SendEmailVerificationAsync(default!, default!, default);
    }

    [Fact]
    public async Task Should_Return_Success_When_User_Is_Inactive()
    {
        var command = new ResendConfirmationEmailCommand("inactive@test.com");
        _userManager.FindByEmailAsync(command.Email)
            .Returns(new ApplicationUser { Email = command.Email, IsActive = false });

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        await _emailService.DidNotReceiveWithAnyArgs()
            .SendEmailVerificationAsync(default!, default!, default);
    }

    [Fact]
    public async Task Should_Return_Success_When_Email_Already_Confirmed()
    {
        var command = new ResendConfirmationEmailCommand("confirmed@test.com");
        _userManager.FindByEmailAsync(command.Email)
            .Returns(new ApplicationUser { Email = command.Email, IsActive = true, EmailConfirmed = true });

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        await _emailService.DidNotReceiveWithAnyArgs()
            .SendEmailVerificationAsync(default!, default!, default);
    }

    [Fact]
    public async Task Should_Generate_Token_And_Send_VerificationLink_When_Valid()
    {
        var command = new ResendConfirmationEmailCommand("user@test.com");
        var user = new ApplicationUser { Email = command.Email, IsActive = true, EmailConfirmed = false };
        _userManager.FindByEmailAsync(command.Email).Returns(user);
        _userManager.GenerateEmailConfirmationTokenAsync(user).Returns("confirm-token-123");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        var expectedLink = $"http://localhost:4200/confirm-email?email={WebUtility.UrlEncode(command.Email)}&token={WebUtility.UrlEncode("confirm-token-123")}";
        await _emailService.Received(1)
            .SendEmailVerificationAsync(command.Email, expectedLink, Arg.Any<CancellationToken>());
    }
}
