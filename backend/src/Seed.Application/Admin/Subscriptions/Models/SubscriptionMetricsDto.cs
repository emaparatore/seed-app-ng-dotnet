namespace Seed.Application.Admin.Subscriptions.Models;

public sealed record SubscriptionMetricsDto(
    decimal Mrr, int ActiveCount, int TrialingCount, decimal ChurnRate);
