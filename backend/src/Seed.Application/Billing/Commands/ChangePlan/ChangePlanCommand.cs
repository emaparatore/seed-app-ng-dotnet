using System.Text.Json.Serialization;
using MediatR;
using Seed.Application.Billing.Models;
using Seed.Application.Common;
using Seed.Domain.Enums;

namespace Seed.Application.Billing.Commands.ChangePlan;

public sealed record ChangePlanCommand(
    Guid PlanId,
    BillingInterval BillingInterval,
    string ReturnUrl) : IRequest<Result<ChangePlanResult>>
{
    [JsonIgnore]
    public Guid UserId { get; init; }

    [JsonIgnore]
    public string? IpAddress { get; init; }

    [JsonIgnore]
    public string? UserAgent { get; init; }
}
