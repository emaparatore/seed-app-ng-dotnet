namespace Seed.Application.Admin.Subscriptions.Models;

public sealed record AdminSubscriptionDto(
    Guid Id, string UserEmail, string PlanName,
    string Status, DateTime CurrentPeriodStart, DateTime CurrentPeriodEnd,
    DateTime? TrialEnd, DateTime? CanceledAt, DateTime CreatedAt);
