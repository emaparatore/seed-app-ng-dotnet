using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using NSubstitute;
using Seed.Application.Admin.Users.Commands.UpdateUser;
using Seed.Application.Common.Interfaces;
using Seed.Domain.Authorization;
using Seed.Domain.Entities;

namespace Seed.UnitTests.Admin.Users;

public class UpdateUserCommandHandlerTests
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAuditService _auditService;
    private readonly UpdateUserCommandHandler _handler;

    public UpdateUserCommandHandlerTests()
    {
        var store = Substitute.For<IUserStore<ApplicationUser>>();
        _userManager = Substitute.For<UserManager<ApplicationUser>>(
            store, null, null, null, null, null, null, null, null);
        _auditService = Substitute.For<IAuditService>();
        _handler = new UpdateUserCommandHandler(_userManager, _auditService);
    }

    [Fact]
    public async Task Should_Update_User_Successfully()
    {
        var userId = Guid.NewGuid();
        var user = new ApplicationUser
        {
            Id = userId, Email = "old@test.com", UserName = "old@test.com",
            FirstName = "Old", LastName = "Name"
        };
        var command = new UpdateUserCommand("New", "Name", "old@test.com") { UserId = userId };

        _userManager.FindByIdAsync(userId.ToString()).Returns(user);
        _userManager.UpdateAsync(user).Returns(IdentityResult.Success);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        user.FirstName.Should().Be("New");
        user.LastName.Should().Be("Name");
    }

    [Fact]
    public async Task Should_Fail_When_User_Not_Found()
    {
        var command = new UpdateUserCommand("New", "Name", "new@test.com")
        {
            UserId = Guid.NewGuid()
        };
        _userManager.FindByIdAsync(command.UserId.ToString()).Returns((ApplicationUser?)null);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("User not found.");
    }

    [Fact]
    public async Task Should_Fail_When_New_Email_Already_Taken()
    {
        var userId = Guid.NewGuid();
        var user = new ApplicationUser
        {
            Id = userId, Email = "old@test.com", UserName = "old@test.com",
            FirstName = "Old", LastName = "Name"
        };
        var command = new UpdateUserCommand("New", "Name", "taken@test.com") { UserId = userId };

        _userManager.FindByIdAsync(userId.ToString()).Returns(user);
        _userManager.FindByEmailAsync("taken@test.com")
            .Returns(new ApplicationUser { Email = "taken@test.com" });

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("A user with this email already exists.");
    }

    [Fact]
    public async Task Should_Log_Audit_On_Success()
    {
        var userId = Guid.NewGuid();
        var user = new ApplicationUser
        {
            Id = userId, Email = "user@test.com", UserName = "user@test.com",
            FirstName = "Old", LastName = "Name"
        };
        var command = new UpdateUserCommand("New", "Name", "user@test.com") { UserId = userId };

        _userManager.FindByIdAsync(userId.ToString()).Returns(user);
        _userManager.UpdateAsync(user).Returns(IdentityResult.Success);

        await _handler.Handle(command, CancellationToken.None);

        await _auditService.Received(1).LogAsync(
            AuditActions.UserUpdated, "User",
            userId.ToString(), Arg.Any<string?>(), Arg.Any<Guid?>(),
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }
}
