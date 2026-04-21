using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using NSubstitute;
using Seed.Api.Authorization;
using Seed.Application.Common.Interfaces;
using Seed.Shared.Configuration;

namespace Seed.UnitTests.Authorization;

public class RequiresPlanAuthorizationHandlerTests
{
    private static IOptions<ModulesSettings> BuildOptions(bool paymentsEnabled)
    {
        var settings = new ModulesSettings
        {
            Payments = new PaymentsModuleSettings { Enabled = paymentsEnabled }
        };
        return Options.Create(settings);
    }

    private static AuthorizationHandlerContext BuildContext(Guid? userId = null)
    {
        var requirements = new List<IAuthorizationRequirement> { new PlanRequirement(["Pro"]) };
        ClaimsPrincipal user;
        if (userId.HasValue)
        {
            var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString())], "Test");
            user = new ClaimsPrincipal(identity);
        }
        else
        {
            user = new ClaimsPrincipal(new ClaimsIdentity());
        }
        return new AuthorizationHandlerContext(requirements, user, null);
    }

    [Fact]
    public async Task ModuleDisabled_ShouldSucceed()
    {
        var service = Substitute.For<ISubscriptionAccessService>();
        var handler = new RequiresPlanAuthorizationHandler(BuildOptions(paymentsEnabled: false), service);
        var requirement = new PlanRequirement(["Pro"]);
        var context = BuildContext(Guid.NewGuid());

        await handler.HandleAsync(context);

        context.HasSucceeded.Should().BeTrue();
        await service.DidNotReceive().UserHasActivePlanAsync(Arg.Any<Guid>(), Arg.Any<string[]>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NoUser_ShouldNotSucceed()
    {
        var service = Substitute.For<ISubscriptionAccessService>();
        var handler = new RequiresPlanAuthorizationHandler(BuildOptions(paymentsEnabled: true), service);
        var context = BuildContext(userId: null);

        await handler.HandleAsync(context);

        context.HasSucceeded.Should().BeFalse();
    }

    [Fact]
    public async Task UserWithActivePlan_ShouldSucceed()
    {
        var userId = Guid.NewGuid();
        var service = Substitute.For<ISubscriptionAccessService>();
        service.UserHasActivePlanAsync(userId, Arg.Any<string[]>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var handler = new RequiresPlanAuthorizationHandler(BuildOptions(paymentsEnabled: true), service);
        var context = BuildContext(userId);

        await handler.HandleAsync(context);

        context.HasSucceeded.Should().BeTrue();
    }

    [Fact]
    public async Task UserWithTrialingPlan_ShouldSucceed()
    {
        var userId = Guid.NewGuid();
        var service = Substitute.For<ISubscriptionAccessService>();
        service.UserHasActivePlanAsync(userId, Arg.Any<string[]>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var handler = new RequiresPlanAuthorizationHandler(BuildOptions(paymentsEnabled: true), service);
        var context = BuildContext(userId);

        await handler.HandleAsync(context);

        context.HasSucceeded.Should().BeTrue();
    }

    [Fact]
    public async Task UserWithWrongPlan_ShouldNotSucceed()
    {
        var userId = Guid.NewGuid();
        var service = Substitute.For<ISubscriptionAccessService>();
        service.UserHasActivePlanAsync(userId, Arg.Any<string[]>(), Arg.Any<CancellationToken>())
            .Returns(false);

        var handler = new RequiresPlanAuthorizationHandler(BuildOptions(paymentsEnabled: true), service);
        var context = BuildContext(userId);

        await handler.HandleAsync(context);

        context.HasSucceeded.Should().BeFalse();
    }

    [Fact]
    public async Task UserWithNoSubscription_ShouldNotSucceed()
    {
        var userId = Guid.NewGuid();
        var service = Substitute.For<ISubscriptionAccessService>();
        service.UserHasActivePlanAsync(userId, Arg.Any<string[]>(), Arg.Any<CancellationToken>())
            .Returns(false);

        var handler = new RequiresPlanAuthorizationHandler(BuildOptions(paymentsEnabled: true), service);
        var context = BuildContext(userId);

        await handler.HandleAsync(context);

        context.HasSucceeded.Should().BeFalse();
    }
}
