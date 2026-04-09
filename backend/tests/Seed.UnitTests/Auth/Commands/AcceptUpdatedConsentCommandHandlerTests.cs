using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using NSubstitute;
using Seed.Application.Auth.Commands.AcceptUpdatedConsent;
using Seed.Application.Common.Interfaces;
using Seed.Domain.Authorization;
using Seed.Domain.Entities;
using Seed.Shared.Configuration;

namespace Seed.UnitTests.Auth.Commands;

public class AcceptUpdatedConsentCommandHandlerTests
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAuditService _auditService;
    private readonly AcceptUpdatedConsentCommandHandler _handler;

    public AcceptUpdatedConsentCommandHandlerTests()
    {
        var store = Substitute.For<IUserStore<ApplicationUser>>();
        _userManager = Substitute.For<UserManager<ApplicationUser>>(
            store, null, null, null, null, null, null, null, null);
        _auditService = Substitute.For<IAuditService>();
        var privacySettings = Options.Create(new PrivacySettings { ConsentVersion = "2.0" });
        _handler = new AcceptUpdatedConsentCommandHandler(_userManager, privacySettings, _auditService);
    }

    [Fact]
    public async Task Should_Update_Consent_Fields_On_Success()
    {
        var userId = Guid.NewGuid();
        var user = new ApplicationUser
        {
            Id = userId, Email = "user@test.com", FirstName = "John", LastName = "Doe",
            ConsentVersion = "1.0"
        };
        _userManager.FindByIdAsync(userId.ToString()).Returns(user);
        _userManager.UpdateAsync(user).Returns(IdentityResult.Success);

        var result = await _handler.Handle(new AcceptUpdatedConsentCommand(userId), CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        user.ConsentVersion.Should().Be("2.0");
        user.PrivacyPolicyAcceptedAt.Should().NotBeNull();
        user.TermsAcceptedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Should_Fail_When_User_Not_Found()
    {
        var userId = Guid.NewGuid();
        _userManager.FindByIdAsync(userId.ToString()).Returns((ApplicationUser?)null);

        var result = await _handler.Handle(new AcceptUpdatedConsentCommand(userId), CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("User not found.");
    }

    [Fact]
    public async Task Should_Log_Audit_Event_On_Success()
    {
        var userId = Guid.NewGuid();
        var user = new ApplicationUser
        {
            Id = userId, Email = "user@test.com", FirstName = "John", LastName = "Doe",
            ConsentVersion = "1.0"
        };
        _userManager.FindByIdAsync(userId.ToString()).Returns(user);
        _userManager.UpdateAsync(user).Returns(IdentityResult.Success);

        await _handler.Handle(new AcceptUpdatedConsentCommand(userId), CancellationToken.None);

        await _auditService.Received(1).LogAsync(
            AuditActions.ConsentGiven, "User", userId.ToString(),
            "Consent re-accepted for version 2.0", userId,
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }
}
