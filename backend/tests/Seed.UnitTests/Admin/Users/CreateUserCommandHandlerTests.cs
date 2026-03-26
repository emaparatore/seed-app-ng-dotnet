using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using NSubstitute;
using Seed.Application.Admin.Users.Commands.CreateUser;
using Seed.Application.Common.Interfaces;
using Seed.Domain.Authorization;
using Seed.Domain.Entities;

namespace Seed.UnitTests.Admin.Users;

public class CreateUserCommandHandlerTests
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly IAuditService _auditService;
    private readonly CreateUserCommandHandler _handler;

    public CreateUserCommandHandlerTests()
    {
        var userStore = Substitute.For<IUserStore<ApplicationUser>>();
        _userManager = Substitute.For<UserManager<ApplicationUser>>(
            userStore, null, null, null, null, null, null, null, null);

        var roleStore = Substitute.For<IRoleStore<ApplicationRole>>();
        _roleManager = Substitute.For<RoleManager<ApplicationRole>>(
            roleStore, null, null, null, null);

        _auditService = Substitute.For<IAuditService>();
        _handler = new CreateUserCommandHandler(_userManager, _roleManager, _auditService);
    }

    [Fact]
    public async Task Should_Create_User_Successfully()
    {
        var command = new CreateUserCommand("new@test.com", "John", "Doe", "Password1", ["Admin"]);
        _userManager.FindByEmailAsync(command.Email).Returns((ApplicationUser?)null);
        _userManager.CreateAsync(Arg.Any<ApplicationUser>(), command.Password)
            .Returns(IdentityResult.Success);
        _roleManager.RoleExistsAsync("Admin").Returns(true);
        _userManager.AddToRoleAsync(Arg.Any<ApplicationUser>(), "Admin")
            .Returns(IdentityResult.Success);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.Data.Should().NotBeEmpty();
        await _auditService.Received(1).LogAsync(
            AuditActions.UserCreated, "User",
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<Guid?>(),
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_Fail_When_Email_Already_Exists()
    {
        var command = new CreateUserCommand("existing@test.com", "John", "Doe", "Password1", []);
        _userManager.FindByEmailAsync(command.Email)
            .Returns(new ApplicationUser { Email = command.Email });

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("A user with this email already exists.");
    }

    [Fact]
    public async Task Should_Fail_When_UserManager_Create_Fails()
    {
        var command = new CreateUserCommand("new@test.com", "John", "Doe", "weak", []);
        _userManager.FindByEmailAsync(command.Email).Returns((ApplicationUser?)null);
        _userManager.CreateAsync(Arg.Any<ApplicationUser>(), command.Password)
            .Returns(IdentityResult.Failed(new IdentityError { Description = "Password too weak" }));

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("Password too weak");
    }

    [Fact]
    public async Task Should_Set_MustChangePassword_To_True()
    {
        var command = new CreateUserCommand("new@test.com", "John", "Doe", "Password1", []);
        _userManager.FindByEmailAsync(command.Email).Returns((ApplicationUser?)null);
        _userManager.CreateAsync(Arg.Any<ApplicationUser>(), command.Password)
            .Returns(callInfo =>
            {
                var user = callInfo.ArgAt<ApplicationUser>(0);
                user.MustChangePassword.Should().BeTrue();
                return IdentityResult.Success;
            });

        await _handler.Handle(command, CancellationToken.None);
    }
}
