namespace Seed.Application.Admin.Plans.Models;

public sealed record CreatePlanFeatureRequest(string Key, string Description, string? LimitValue, int SortOrder);
