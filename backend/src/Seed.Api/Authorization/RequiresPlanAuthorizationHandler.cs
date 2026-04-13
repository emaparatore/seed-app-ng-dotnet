using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Seed.Application.Common.Interfaces;
using Seed.Shared.Configuration;

namespace Seed.Api.Authorization;

public class RequiresPlanAuthorizationHandler(
    IOptions<ModulesSettings> modulesOptions,
    ISubscriptionAccessService subscriptionAccessService)
    : AuthorizationHandler<PlanRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PlanRequirement requirement)
    {
        if (!modulesOptions.Value.Payments.Enabled)
        {
            context.Succeed(requirement);
            return;
        }

        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
            return;

        var hasActivePlan = await subscriptionAccessService.UserHasActivePlanAsync(
            Guid.Parse(userId), requirement.PlanNames);

        if (hasActivePlan)
            context.Succeed(requirement);
    }
}
