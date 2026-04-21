using System.Text.Json.Serialization;
using MediatR;
using Seed.Application.Billing.Models;
using Seed.Application.Common;
using Seed.Domain.Enums;

namespace Seed.Application.Billing.Commands.CreateCheckoutSession;

public sealed record CreateCheckoutSessionCommand(
    Guid PlanId,
    BillingInterval BillingInterval,
    string SuccessUrl,
    string CancelUrl) : IRequest<Result<CheckoutSessionResponse>>
{
    [JsonIgnore]
    public Guid UserId { get; init; }

    [JsonIgnore]
    public string? IpAddress { get; init; }

    [JsonIgnore]
    public string? UserAgent { get; init; }
}
