using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using NSubstitute;
using Seed.Application.Auth.Queries.GetCurrentUser;
using Seed.Application.Common.Interfaces;
using Seed.Domain.Entities;

namespace Seed.UnitTests.Auth.Queries;

public class GetCurrentUserQueryHandlerTests
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IPermissionService _permissionService;
    private readonly GetCurrentUserQueryHandler _handler;

    public GetCurrentUserQueryHandlerTests()
    {
        var store = Substitute.For<IUserStore<ApplicationUser>>();
        _userManager = Substitute.For<UserManager<ApplicationUser>>(
            store, null, null, null, null, null, null, null, null);
        _permissionService = Substitute.For<IPermissionService>();
        _handler = new GetCurrentUserQueryHandler(_userManager, _permissionService);
    }

    [Fact]
    public async Task Should_Fail_When_User_Not_Found()
    {
        var query = new GetCurrentUserQuery(Guid.NewGuid());
        _userManager.FindByIdAsync(query.UserId.ToString()).Returns((ApplicationUser?)null);

        var result = await _handler.Handle(query, CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("User not found.");
    }

    [Fact]
    public async Task Should_Fail_When_User_Is_Not_Active()
    {
        var userId = Guid.NewGuid();
        var query = new GetCurrentUserQuery(userId);
        _userManager.FindByIdAsync(userId.ToString())
            .Returns(new ApplicationUser { Id = userId, IsActive = false });

        var result = await _handler.Handle(query, CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("User not found.");
    }

    [Fact]
    public async Task Should_Return_MeResponse_When_User_Exists_And_Active()
    {
        var userId = Guid.NewGuid();
        var query = new GetCurrentUserQuery(userId);
        var user = new ApplicationUser
        {
            Id = userId,
            Email = "user@test.com",
            FirstName = "John",
            LastName = "Doe",
            IsActive = true
        };
        var permissions = new HashSet<string> { "users.read" };
        _userManager.FindByIdAsync(userId.ToString()).Returns(user);
        _permissionService.GetPermissionsAsync(userId).Returns(permissions);

        var result = await _handler.Handle(query, CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.Data!.Id.Should().Be(userId);
        result.Data.Email.Should().Be("user@test.com");
        result.Data.FirstName.Should().Be("John");
        result.Data.LastName.Should().Be("Doe");
        result.Data.Permissions.Should().Contain("users.read");
    }
}
