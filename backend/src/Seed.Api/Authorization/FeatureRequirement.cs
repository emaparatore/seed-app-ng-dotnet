using Microsoft.AspNetCore.Authorization;

namespace Seed.Api.Authorization;

public class FeatureRequirement(string featureKey) : IAuthorizationRequirement
{
    public string FeatureKey { get; } = featureKey;
}
