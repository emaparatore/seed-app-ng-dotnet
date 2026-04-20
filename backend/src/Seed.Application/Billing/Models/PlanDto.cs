namespace Seed.Application.Billing.Models;

public sealed record PlanDto(
    Guid Id, string Name, string? Description,
    decimal MonthlyPrice, decimal YearlyPrice,
    int TrialDays, bool IsFreeTier, bool IsDefault, bool IsPopular,
    int SortOrder, IReadOnlyList<PlanFeatureDto> Features);
