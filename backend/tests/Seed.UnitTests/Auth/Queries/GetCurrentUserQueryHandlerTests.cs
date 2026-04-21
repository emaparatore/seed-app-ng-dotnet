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
    private readonly ISubscriptionInfoService _subscriptionInfoService;
    private readonly GetCurrentUserQueryHandler _handler;

    public GetCurrentUserQueryHandlerTests()
    {
        var store = Substitute.For<IUserStore<ApplicationUser>>();
        _userManager = Substitute.For<UserManager<ApplicationUser>>(
            store, null, null, null, null, null, null, null, null);
        _permissionService = Substitute.For<IPermissionService>();
        _subscriptionInfoService = Substitute.For<ISubscriptionInfoService>();
        _handler = new GetCurrentUserQueryHandler(_userManager, _permissionService, _subscriptionInfoService);
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
        _subscriptionInfoService.GetUserSubscriptionInfoAsync(userId, Arg.Any<CancellationToken>())
            .Returns((SubscriptionInfoDto?)null);

        var result = await _handler.Handle(query, CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.Data!.Id.Should().Be(userId);
        result.Data.Email.Should().Be("user@test.com");
        result.Data.FirstName.Should().Be("John");
        result.Data.LastName.Should().Be("Doe");
        result.Data.Permissions.Should().Contain("users.read");
        result.Data.Subscription.Should().BeNull();
    }

    [Fact]
    public async Task Should_Include_Subscription_Info_When_Service_Returns_Data()
    {
        var userId = Guid.NewGuid();
        var query = new GetCurrentUserQuery(userId);
        var user = new ApplicationUser
        {
            Id = userId,
            Email = "user@test.com",
            FirstName = "Jane",
            LastName = "Doe",
            IsActive = true
        };
        var trialEnd = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc);
        var subscriptionInfo = new SubscriptionInfoDto(
            "Pro",
            new[] { "feature-a", "feature-b" }.ToList().AsReadOnly(),
            "Active",
            trialEnd);

        _userManager.FindByIdAsync(userId.ToString()).Returns(user);
        _permissionService.GetPermissionsAsync(userId).Returns(new HashSet<string>());
        _subscriptionInfoService.GetUserSubscriptionInfoAsync(userId, Arg.Any<CancellationToken>())
            .Returns(subscriptionInfo);

        var result = await _handler.Handle(query, CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.Data!.Subscription.Should().NotBeNull();
        result.Data.Subscription!.CurrentPlan.Should().Be("Pro");
        result.Data.Subscription.PlanFeatures.Should().BeEquivalentTo(new[] { "feature-a", "feature-b" });
        result.Data.Subscription.SubscriptionStatus.Should().Be("Active");
        result.Data.Subscription.TrialEndsAt.Should().Be(trialEnd);
    }

    [Fact]
    public async Task Should_Return_Null_Subscription_When_Service_Returns_Null()
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
        _userManager.FindByIdAsync(userId.ToString()).Returns(user);
        _permissionService.GetPermissionsAsync(userId).Returns(new HashSet<string>());
        _subscriptionInfoService.GetUserSubscriptionInfoAsync(userId, Arg.Any<CancellationToken>())
            .Returns((SubscriptionInfoDto?)null);

        var result = await _handler.Handle(query, CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.Data!.Subscription.Should().BeNull();
    }
}
