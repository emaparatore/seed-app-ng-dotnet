using System.Text.Json.Serialization;
using MediatR;
using Seed.Application.Common;

namespace Seed.Application.Admin.Roles.Commands.CreateRole;

public sealed record CreateRoleCommand(
    string Name,
    string? Description,
    string[] PermissionNames) : IRequest<Result<Guid>>
{
    [JsonIgnore]
    public Guid CurrentUserId { get; init; }

    [JsonIgnore]
    public string? IpAddress { get; init; }

    [JsonIgnore]
    public string? UserAgent { get; init; }
}
