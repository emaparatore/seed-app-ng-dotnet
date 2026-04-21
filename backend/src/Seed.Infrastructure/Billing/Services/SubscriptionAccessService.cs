using Microsoft.EntityFrameworkCore;
using Seed.Application.Common.Interfaces;
using Seed.Domain.Enums;
using Seed.Infrastructure.Persistence;

namespace Seed.Infrastructure.Billing.Services;

public sealed class SubscriptionAccessService(ApplicationDbContext dbContext) : ISubscriptionAccessService
{
    private static readonly SubscriptionStatus[] ActiveStatuses = [SubscriptionStatus.Active, SubscriptionStatus.Trialing];

    public Task<bool> UserHasActivePlanAsync(Guid userId, string[] planNames, CancellationToken ct = default) =>
        dbContext.UserSubscriptions
            .Where(s => s.UserId == userId && ActiveStatuses.Contains(s.Status))
            .AnyAsync(s => planNames.Contains(s.Plan.Name), ct);

    public Task<bool> UserHasFeatureAsync(Guid userId, string featureKey, CancellationToken ct = default) =>
        dbContext.UserSubscriptions
            .Where(s => s.UserId == userId && ActiveStatuses.Contains(s.Status))
            .AnyAsync(s => s.Plan.Features.Any(f => f.Key == featureKey), ct);
}
