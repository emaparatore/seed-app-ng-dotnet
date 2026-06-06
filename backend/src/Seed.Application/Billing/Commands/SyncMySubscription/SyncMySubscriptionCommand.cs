using System.Text.Json.Serialization;
using MediatR;
using Seed.Application.Billing.Models;
using Seed.Application.Common;

namespace Seed.Application.Billing.Commands.SyncMySubscription;

public sealed record SyncMySubscriptionCommand : IRequest<Result<SyncSubscriptionResponse>>
{
    [JsonIgnore]
    public Guid UserId { get; init; }

    [JsonIgnore]
    public string? IpAddress { get; init; }

    [JsonIgnore]
    public string? UserAgent { get; init; }
}
