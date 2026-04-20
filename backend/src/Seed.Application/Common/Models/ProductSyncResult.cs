namespace Seed.Application.Common.Models;

public sealed record ProductSyncResult(
    string ProductId,
    string MonthlyPriceId,
    string YearlyPriceId);
