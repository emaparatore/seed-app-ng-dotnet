using Seed.Application.Billing.Models;

namespace Seed.Application.Admin.Plans.Models;

public sealed record AdminPlanDto(
    Guid Id, string Name, string? Description,
    decimal MonthlyPrice, decimal YearlyPrice,
    string? StripePriceIdMonthly, string? StripePriceIdYearly, string? StripeProductId,
    int TrialDays, bool IsFreeTier, bool IsDefault, bool IsPopular,
    string Status, int SortOrder,
    DateTime CreatedAt, DateTime UpdatedAt,
    int SubscriberCount,
    IReadOnlyList<PlanFeatureDto> Features);
