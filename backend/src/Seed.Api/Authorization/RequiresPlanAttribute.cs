using Microsoft.AspNetCore.Authorization;

namespace Seed.Api.Authorization;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public class RequiresPlanAttribute : AuthorizeAttribute
{
    public RequiresPlanAttribute(params string[] planNames)
        : base($"Plan:{string.Join(',', planNames)}") { }
}
