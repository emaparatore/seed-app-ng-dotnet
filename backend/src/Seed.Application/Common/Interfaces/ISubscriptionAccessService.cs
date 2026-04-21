namespace Seed.Application.Common.Interfaces;

public interface ISubscriptionAccessService
{
    Task<bool> UserHasActivePlanAsync(Guid userId, string[] planNames, CancellationToken ct = default);
    Task<bool> UserHasFeatureAsync(Guid userId, string featureKey, CancellationToken ct = default);
}
