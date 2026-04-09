using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using NSubstitute;
using Seed.Application.Auth.Commands.Register;
using Seed.Application.Common.Interfaces;
using Seed.Domain.Authorization;
using Seed.Domain.Entities;
using Seed.Shared.Configuration;

namespace Seed.UnitTests.Auth.Commands;

public class RegisterCommandHandlerTests
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IEmailService _emailService;
    private readonly IAuditService _auditService;
    private readonly RegisterCommandHandler _handler;

    public RegisterCommandHandlerTests()
    {
        var store = Substitute.For<IUserStore<ApplicationUser>>();
        _userManager = Substitute.For<UserManager<ApplicationUser>>(
            store, null, null, null, null, null, null, null, null);
        _emailService = Substitute.For<IEmailService>();
        var clientSettings = Options.Create(new ClientSettings { BaseUrl = "http://localhost:4200" });
        var privacySettings = Options.Create(new PrivacySettings { ConsentVersion = "1.0" });
        _auditService = Substitute.For<IAuditService>();
        _handler = new RegisterCommandHandler(_userManager, _emailService, clientSettings, privacySettings, _auditService);
    }

    [Fact]
    public async Task Should_Fail_When_Email_Already_Exists()
    {
        var command = new RegisterCommand("existing@test.com", "Password1", "John", "Doe", true, true);
        _userManager.FindByEmailAsync(command.Email)
            .Returns(new ApplicationUser { Email = command.Email });

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("A user with this email already exists.");
    }

    [Fact]
    public async Task Should_Fail_When_UserManager_CreateAsync_Fails()
    {
        var command = new RegisterCommand("new@test.com", "Password1", "John", "Doe", true, true);
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
        var command = new RegisterCommand("new@test.com", "Password1", "John", "Doe", true, true);

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
        var command = new RegisterCommand("new@test.com", "Password1", "John", "Doe", true, true);

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

    [Fact]
    public async Task Should_Save_Consent_Timestamp_And_Version_On_Successful_Registration()
    {
        var command = new RegisterCommand("new@test.com", "Password1", "John", "Doe", true, true);
        ApplicationUser? capturedUser = null;

        _userManager.FindByEmailAsync(command.Email).Returns((ApplicationUser?)null);
        _userManager.CreateAsync(Arg.Do<ApplicationUser>(u => capturedUser = u), command.Password)
            .Returns(IdentityResult.Success);
        _userManager.GenerateEmailConfirmationTokenAsync(Arg.Any<ApplicationUser>())
            .Returns("confirm-token-123");

        await _handler.Handle(command, CancellationToken.None);

        capturedUser.Should().NotBeNull();
        capturedUser!.PrivacyPolicyAcceptedAt.Should().NotBeNull();
        capturedUser.TermsAcceptedAt.Should().NotBeNull();
        capturedUser.ConsentVersion.Should().Be("1.0");
        await _auditService.Received(1).LogAsync(
            AuditActions.ConsentGiven,
            "User",
            Arg.Any<string>(),
            Arg.Is<string>(d => d.Contains("1.0")),
            Arg.Any<Guid?>(),
            Arg.Any<string?>(),
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(false, false)]
    public async Task Validator_Should_Reject_Registration_Without_Consent(bool acceptPrivacy, bool acceptTerms)
    {
        var command = new RegisterCommand("new@test.com", "Password1", "John", "Doe", acceptPrivacy, acceptTerms);
        var validator = new RegisterCommandValidator();

        var result = await validator.ValidateAsync(command);

        result.IsValid.Should().BeFalse();
        if (!acceptPrivacy)
            result.Errors.Should().Contain(e => e.PropertyName == "AcceptPrivacyPolicy");
        if (!acceptTerms)
            result.Errors.Should().Contain(e => e.PropertyName == "AcceptTermsOfService");
    }
}
