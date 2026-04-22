namespace Seed.Application.Common.Models;

public sealed record CheckoutSessionDetails(
    string SessionId,
    string Status,
    string PaymentStatus,
    string? SubscriptionId,
    string? CustomerId,
    IReadOnlyDictionary<string, string> Metadata);
