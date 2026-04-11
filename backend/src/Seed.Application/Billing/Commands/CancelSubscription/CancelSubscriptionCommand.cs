using System.Text.Json.Serialization;
using MediatR;
using Seed.Application.Common;

namespace Seed.Application.Billing.Commands.CancelSubscription;

public sealed record CancelSubscriptionCommand() : IRequest<Result<bool>>
{
    [JsonIgnore]
    public Guid UserId { get; init; }

    [JsonIgnore]
    public string? IpAddress { get; init; }

    [JsonIgnore]
    public string? UserAgent { get; init; }
}
