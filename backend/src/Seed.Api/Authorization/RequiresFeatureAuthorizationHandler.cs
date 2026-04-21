using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Seed.Application.Common.Interfaces;
using Seed.Shared.Configuration;

namespace Seed.Api.Authorization;

public class RequiresFeatureAuthorizationHandler(
    IOptions<ModulesSettings> modulesOptions,
    ISubscriptionAccessService subscriptionAccessService)
    : AuthorizationHandler<FeatureRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        FeatureRequirement requirement)
    {
        if (!modulesOptions.Value.Payments.Enabled)
        {
            context.Succeed(requirement);
            return;
        }

        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
            return;

        var hasFeature = await subscriptionAccessService.UserHasFeatureAsync(
            Guid.Parse(userId), requirement.FeatureKey);

        if (hasFeature)
            context.Succeed(requirement);
    }
}
