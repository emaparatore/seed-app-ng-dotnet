using System.Text.Json.Serialization;
using MediatR;
using Seed.Application.Common;

namespace Seed.Application.Admin.Users.Commands.DeleteUser;

public sealed record DeleteUserCommand(Guid UserId) : IRequest<Result<bool>>
{
    [JsonIgnore]
    public Guid CurrentUserId { get; init; }

    [JsonIgnore]
    public string? IpAddress { get; init; }

    [JsonIgnore]
    public string? UserAgent { get; init; }
}
