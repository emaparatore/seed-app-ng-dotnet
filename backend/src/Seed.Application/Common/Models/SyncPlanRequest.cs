namespace Seed.Application.Common.Models;

public sealed record SyncPlanRequest(
    string? ProductId,
    string Name,
    string? Description,
    long MonthlyPriceInCents,
    long YearlyPriceInCents,
    string? ExistingMonthlyPriceId,
    string? ExistingYearlyPriceId);
