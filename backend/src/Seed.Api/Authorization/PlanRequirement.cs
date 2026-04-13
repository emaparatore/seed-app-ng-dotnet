using Microsoft.AspNetCore.Authorization;

namespace Seed.Api.Authorization;

public class PlanRequirement(string[] planNames) : IAuthorizationRequirement
{
    public string[] PlanNames { get; } = planNames;
}
