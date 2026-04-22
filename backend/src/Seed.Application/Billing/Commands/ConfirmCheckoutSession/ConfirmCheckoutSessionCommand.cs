using System.Text.Json.Serialization;
using MediatR;
using Seed.Application.Billing.Models;
using Seed.Application.Common;

namespace Seed.Application.Billing.Commands.ConfirmCheckoutSession;

public sealed record ConfirmCheckoutSessionCommand(string SessionId) : IRequest<Result<CheckoutConfirmationResponse>>
{
    [JsonIgnore]
    public Guid UserId { get; init; }

    [JsonIgnore]
    public string? IpAddress { get; init; }

    [JsonIgnore]
    public string? UserAgent { get; init; }
}
