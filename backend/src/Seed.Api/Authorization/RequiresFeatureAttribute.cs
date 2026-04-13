using Microsoft.AspNetCore.Authorization;

namespace Seed.Api.Authorization;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public class RequiresFeatureAttribute : AuthorizeAttribute
{
    public RequiresFeatureAttribute(string featureKey)
        : base($"Feature:{featureKey}") { }
}
