namespace Seed.Application.Billing.Models;

public sealed record SyncSubscriptionResponse(bool Synced, string Status, string? PlanName);
