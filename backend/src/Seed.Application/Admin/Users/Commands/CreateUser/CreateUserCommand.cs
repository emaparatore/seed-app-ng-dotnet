using System.Text.Json.Serialization;
using MediatR;
using Seed.Application.Common;

namespace Seed.Application.Admin.Users.Commands.CreateUser;

public sealed record CreateUserCommand(
    string Email,
    string FirstName,
    string LastName,
    string Password,
    string[] RoleNames) : IRequest<Result<Guid>>
{
    [JsonIgnore]
    public Guid CurrentUserId { get; init; }

    [JsonIgnore]
    public string? IpAddress { get; init; }

    [JsonIgnore]
    public string? UserAgent { get; init; }
}
