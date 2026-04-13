using Seed.Application.Common.Interfaces;

namespace Seed.Infrastructure.Billing.Services;

public sealed class AlwaysAllowSubscriptionAccessService : ISubscriptionAccessService
{
    public Task<bool> UserHasActivePlanAsync(Guid userId, string[] planNames, CancellationToken ct = default) =>
        Task.FromResult(true);

    public Task<bool> UserHasFeatureAsync(Guid userId, string featureKey, CancellationToken ct = default) =>
        Task.FromResult(true);
}
