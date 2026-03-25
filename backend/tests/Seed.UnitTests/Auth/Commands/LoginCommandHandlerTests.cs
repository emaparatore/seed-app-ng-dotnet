using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using NSubstitute;
using Seed.Application.Auth.Commands.Login;
using Seed.Application.Common.Interfaces;
using Seed.Application.Common.Models;
using Seed.Domain.Authorization;
using Seed.Domain.Entities;

namespace Seed.UnitTests.Auth.Commands;

public class LoginCommandHandlerTests
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ITokenService _tokenService;
    private readonly IPermissionService _permissionService;
    private readonly IAuditService _auditService;
    private readonly LoginCommandHandler _handler;

    public LoginCommandHandlerTests()
    {
        var store = Substitute.For<IUserStore<ApplicationUser>>();
        _userManager = Substitute.For<UserManager<ApplicationUser>>(
            store, null, null, null, null, null, null, null, null);
        _tokenService = Substitute.For<ITokenService>();
        _permissionService = Substitute.For<IPermissionService>();
        _auditService = Substitute.For<IAuditService>();
        _permissionService.GetPermissionsAsync(Arg.Any<Guid>())
            .Returns(new HashSet<string>() as IReadOnlySet<string>);
        _userManager.GetRolesAsync(Arg.Any<ApplicationUser>())
            .Returns(new List<string>());
        _handler = new LoginCommandHandler(_userManager, _tokenService, _permissionService, _auditService);
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
    public async Task Should_Fail_When_Email_Not_Verified()
    {
        var command = new LoginCommand("unverified@test.com", "Password1");
        var user = new ApplicationUser { Email = command.Email, IsActive = true, EmailConfirmed = false };
        _userManager.FindByEmailAsync(command.Email).Returns(user);
        _userManager.CheckPasswordAsync(user, command.Password).Returns(true);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Errors[0].Should().Contain("verify your email");
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
            IsActive = true,
            EmailConfirmed = true
        };

        _userManager.FindByEmailAsync(command.Email).Returns(user);
        _userManager.CheckPasswordAsync(user, command.Password).Returns(true);
        _userManager.GetRolesAsync(user).Returns(new List<string> { "User" });

        var tokenResult = new TokenResult("access-token", "refresh-token", DateTime.UtcNow.AddMinutes(15), userId);
        _tokenService.GenerateTokensAsync(user, Arg.Any<IList<string>>()).Returns(tokenResult);

        var permissions = new HashSet<string> { "Users.Read" } as IReadOnlySet<string>;
        _permissionService.GetPermissionsAsync(userId).Returns(permissions);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.AccessToken.Should().Be("access-token");
        result.Data.User.Email.Should().Be(command.Email);
        result.Data.User.Roles.Should().Contain("User");
        result.Data.Permissions.Should().Contain("Users.Read");
        result.Data.MustChangePassword.Should().BeFalse();
    }

    [Fact]
    public async Task Should_Return_MustChangePassword_True_When_Flag_Is_Set()
    {
        var command = new LoginCommand("admin@test.com", "Password1");
        var userId = Guid.NewGuid();
        var user = new ApplicationUser
        {
            Id = userId,
            Email = command.Email,
            FirstName = "Admin",
            LastName = "User",
            IsActive = true,
            EmailConfirmed = true,
            MustChangePassword = true
        };

        _userManager.FindByEmailAsync(command.Email).Returns(user);
        _userManager.CheckPasswordAsync(user, command.Password).Returns(true);
        _userManager.GetRolesAsync(user).Returns(new List<string>());

        var tokenResult = new TokenResult("access-token", "refresh-token", DateTime.UtcNow.AddMinutes(15), userId);
        _tokenService.GenerateTokensAsync(user, Arg.Any<IList<string>>()).Returns(tokenResult);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.Data!.MustChangePassword.Should().BeTrue();
    }

    [Fact]
    public async Task Should_Log_LoginFailed_Audit_When_User_Not_Found()
    {
        var command = new LoginCommand("nobody@test.com", "Password1");
        _userManager.FindByEmailAsync(command.Email).Returns((ApplicationUser?)null);

        await _handler.Handle(command, CancellationToken.None);

        await _auditService.Received(1).LogAsync(
            AuditActions.LoginFailed, "User",
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<Guid?>(),
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_Log_LoginFailed_Audit_When_Password_Is_Wrong()
    {
        var command = new LoginCommand("user@test.com", "WrongPass1");
        var user = new ApplicationUser { Email = command.Email, IsActive = true };
        _userManager.FindByEmailAsync(command.Email).Returns(user);
        _userManager.CheckPasswordAsync(user, command.Password).Returns(false);

        await _handler.Handle(command, CancellationToken.None);

        await _auditService.Received(1).LogAsync(
            AuditActions.LoginFailed, "User",
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<Guid?>(),
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_Log_LoginSuccess_Audit_On_Successful_Login()
    {
        var command = new LoginCommand("user@test.com", "Password1");
        var userId = Guid.NewGuid();
        var user = new ApplicationUser
        {
            Id = userId, Email = command.Email, FirstName = "John", LastName = "Doe",
            IsActive = true, EmailConfirmed = true
        };

        _userManager.FindByEmailAsync(command.Email).Returns(user);
        _userManager.CheckPasswordAsync(user, command.Password).Returns(true);
        _userManager.GetRolesAsync(user).Returns(new List<string>());
        var tokenResult = new TokenResult("access-token", "refresh-token", DateTime.UtcNow.AddMinutes(15), userId);
        _tokenService.GenerateTokensAsync(user, Arg.Any<IList<string>>()).Returns(tokenResult);

        await _handler.Handle(command, CancellationToken.None);

        await _auditService.Received(1).LogAsync(
            AuditActions.LoginSuccess, "User",
            userId.ToString(), Arg.Any<string?>(), userId,
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }
}
