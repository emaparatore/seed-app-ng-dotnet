namespace Seed.Application.Common.Models;

public sealed record SubscriptionDetails(
    string SubscriptionId,
    string CustomerId,
    string Status,
    string PriceId,
    DateTime CurrentPeriodStart,
    DateTime CurrentPeriodEnd,
    DateTime? TrialEnd,
    bool CancelAtPeriodEnd);
