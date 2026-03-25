using System.Text.Json.Serialization;
using MediatR;
using Seed.Application.Common;

namespace Seed.Application.Admin.Roles.Commands.UpdateRole;

public sealed record UpdateRoleCommand(
    string Name,
    string? Description,
    string[] PermissionNames) : IRequest<Result<bool>>
{
    [JsonIgnore]
    public Guid RoleId { get; init; }

    [JsonIgnore]
    public Guid CurrentUserId { get; init; }

    [JsonIgnore]
    public string? IpAddress { get; init; }

    [JsonIgnore]
    public string? UserAgent { get; init; }
}
