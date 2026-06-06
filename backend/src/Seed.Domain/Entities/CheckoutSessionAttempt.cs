using Seed.Domain.Enums;

namespace Seed.Domain.Entities;

public class CheckoutSessionAttempt
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid PlanId { get; set; }
    public CheckoutSessionAttemptStatus Status { get; set; } = CheckoutSessionAttemptStatus.Pending;
    public string? StripeSessionId { get; set; }
    public string? StripeSubscriptionId { get; set; }
    public string? StripeCustomerId { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? FailureReason { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ApplicationUser User { get; set; } = null!;
    public SubscriptionPlan Plan { get; set; } = null!;
}
