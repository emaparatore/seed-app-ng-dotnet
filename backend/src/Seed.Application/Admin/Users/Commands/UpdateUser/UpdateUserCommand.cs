using System.Text.Json.Serialization;
using MediatR;
using Seed.Application.Common;

namespace Seed.Application.Admin.Users.Commands.UpdateUser;

public sealed record UpdateUserCommand(
    string FirstName,
    string LastName,
    string Email) : IRequest<Result<bool>>
{
    [JsonIgnore]
    public Guid UserId { get; init; }

    [JsonIgnore]
    public Guid CurrentUserId { get; init; }

    [JsonIgnore]
    public string? IpAddress { get; init; }

    [JsonIgnore]
    public string? UserAgent { get; init; }
}
