using System.Text.Json.Serialization;
using MediatR;
using Seed.Application.Common;

namespace Seed.Application.Admin.Roles.Commands.DeleteRole;

public sealed record DeleteRoleCommand(Guid RoleId) : IRequest<Result<bool>>
{
    [JsonIgnore]
    public Guid CurrentUserId { get; init; }

    [JsonIgnore]
    public string? IpAddress { get; init; }

    [JsonIgnore]
    public string? UserAgent { get; init; }
}
