using System.Text.Json.Serialization;
using MediatR;
using Seed.Application.Billing.Models;
using Seed.Application.Common;

namespace Seed.Application.Billing.Commands.CreatePortalSession;

public sealed record CreatePortalSessionCommand(
    string ReturnUrl) : IRequest<Result<PortalSessionResponse>>
{
    [JsonIgnore]
    public Guid UserId { get; init; }

    [JsonIgnore]
    public string? IpAddress { get; init; }

    [JsonIgnore]
    public string? UserAgent { get; init; }
}
