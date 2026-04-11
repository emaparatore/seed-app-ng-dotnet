namespace Seed.Application.Billing.Models;

public sealed record UserSubscriptionDto(
    Guid Id, string PlanName, string? PlanDescription,
    string Status, decimal MonthlyPrice, decimal YearlyPrice,
    DateTime CurrentPeriodStart, DateTime CurrentPeriodEnd,
    DateTime? TrialEnd, DateTime? CanceledAt,
    bool IsFreeTier, IReadOnlyList<PlanFeatureDto> Features);
