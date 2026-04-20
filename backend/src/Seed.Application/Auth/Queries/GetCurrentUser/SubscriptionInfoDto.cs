namespace Seed.Application.Auth.Queries.GetCurrentUser;

public sealed record SubscriptionInfoDto(
    string CurrentPlan,
    IReadOnlyList<string> PlanFeatures,
    string SubscriptionStatus,
    DateTime? TrialEndsAt);
