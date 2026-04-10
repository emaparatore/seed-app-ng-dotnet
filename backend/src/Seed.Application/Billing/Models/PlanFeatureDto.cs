namespace Seed.Application.Billing.Models;

public sealed record PlanFeatureDto(
    Guid Id, string Key, string Description, string? LimitValue, int SortOrder);
