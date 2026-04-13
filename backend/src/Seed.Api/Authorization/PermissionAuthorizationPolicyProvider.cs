using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace Seed.Api.Authorization;

public class PermissionAuthorizationPolicyProvider(IOptions<AuthorizationOptions> options)
    : DefaultAuthorizationPolicyProvider(options)
{
    private const string PermissionPrefix = "Permission:";
    private const string PlanPrefix = "Plan:";
    private const string FeaturePrefix = "Feature:";

    public override async Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (policyName.StartsWith(PermissionPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var permission = policyName[PermissionPrefix.Length..];
            return new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .AddRequirements(new PermissionRequirement(permission))
                .Build();
        }

        if (policyName.StartsWith(PlanPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var planNames = policyName[PlanPrefix.Length..].Split(',', StringSplitOptions.RemoveEmptyEntries);
            return new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .AddRequirements(new PlanRequirement(planNames))
                .Build();
        }

        if (policyName.StartsWith(FeaturePrefix, StringComparison.OrdinalIgnoreCase))
        {
            var featureKey = policyName[FeaturePrefix.Length..];
            return new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .AddRequirements(new FeatureRequirement(featureKey))
                .Build();
        }

        return await base.GetPolicyAsync(policyName);
    }
}
