namespace Seed.Domain.Entities;

public class PlanFeature
{
    public Guid Id { get; set; }
    public Guid PlanId { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? LimitValue { get; set; }
    public int SortOrder { get; set; }

    public SubscriptionPlan Plan { get; set; } = null!;
}
