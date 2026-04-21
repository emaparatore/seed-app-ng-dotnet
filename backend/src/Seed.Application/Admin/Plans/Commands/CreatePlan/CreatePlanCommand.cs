using System.Text.Json.Serialization;
using MediatR;
using Seed.Application.Admin.Plans.Models;
using Seed.Application.Common;

namespace Seed.Application.Admin.Plans.Commands.CreatePlan;

public sealed record CreatePlanCommand(
    string Name,
    string? Description,
    decimal MonthlyPrice,
    decimal YearlyPrice,
    int TrialDays,
    bool IsFreeTier,
    bool IsDefault,
    bool IsPopular,
    int SortOrder,
    List<CreatePlanFeatureRequest> Features) : IRequest<Result<Guid>>
{
    [JsonIgnore]
    public Guid CurrentUserId { get; init; }

    [JsonIgnore]
    public string? IpAddress { get; init; }

    [JsonIgnore]
    public string? UserAgent { get; init; }
}
