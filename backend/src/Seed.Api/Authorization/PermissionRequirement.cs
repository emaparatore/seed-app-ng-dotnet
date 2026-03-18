using Microsoft.AspNetCore.Authorization;

namespace Seed.Api.Authorization;

public class PermissionRequirement(string permission) : IAuthorizationRequirement
{
    public string Permission { get; } = permission;
}
