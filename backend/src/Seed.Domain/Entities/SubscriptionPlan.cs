using Seed.Domain.Enums;

namespace Seed.Domain.Entities;

public class SubscriptionPlan
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal MonthlyPrice { get; set; }
    public decimal YearlyPrice { get; set; }
    public string? StripePriceIdMonthly { get; set; }
    public string? StripePriceIdYearly { get; set; }
    public string? StripeProductId { get; set; }
    public int TrialDays { get; set; }
    public bool IsFreeTier { get; set; }
    public bool IsDefault { get; set; }
    public bool IsPopular { get; set; }
    public PlanStatus Status { get; set; } = PlanStatus.Active;
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<PlanFeature> Features { get; set; } = [];
    public ICollection<UserSubscription> Subscriptions { get; set; } = [];
}
