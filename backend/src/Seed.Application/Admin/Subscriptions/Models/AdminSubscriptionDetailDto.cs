namespace Seed.Application.Admin.Subscriptions.Models;

public sealed record AdminSubscriptionDetailDto(
    Guid Id, Guid UserId, string UserEmail, string UserFullName,
    Guid PlanId, string PlanName, decimal MonthlyPrice, decimal YearlyPrice,
    string Status, string? StripeSubscriptionId, string? StripeCustomerId,
    DateTime CurrentPeriodStart, DateTime CurrentPeriodEnd,
    DateTime? TrialEnd, DateTime? CanceledAt,
    DateTime CreatedAt, DateTime UpdatedAt);
