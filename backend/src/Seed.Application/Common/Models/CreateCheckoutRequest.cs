namespace Seed.Application.Common.Models;

public sealed record CreateCheckoutRequest(
    string PriceId,
    string CustomerEmail,
    string? CustomerId,
    string SuccessUrl,
    string CancelUrl,
    int? TrialDays,
    Dictionary<string, string>? Metadata);
