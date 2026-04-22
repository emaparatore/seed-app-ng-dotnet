namespace Seed.Application.Admin.Users.Models;

public sealed record AdminUserSubscriptionDto(
    string CurrentPlan,
    string SubscriptionStatus,
    DateTime? TrialEndsAt);
