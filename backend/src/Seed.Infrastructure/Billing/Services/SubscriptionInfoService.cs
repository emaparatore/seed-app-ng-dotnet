using Microsoft.EntityFrameworkCore;
using Seed.Application.Auth.Queries.GetCurrentUser;
using Seed.Application.Common.Interfaces;
using Seed.Domain.Enums;
using Seed.Infrastructure.Persistence;

namespace Seed.Infrastructure.Billing.Services;

public sealed class SubscriptionInfoService(ApplicationDbContext dbContext) : ISubscriptionInfoService
{
    private static readonly SubscriptionStatus[] ActiveStatuses = [SubscriptionStatus.Active, SubscriptionStatus.Trialing];

    public async Task<SubscriptionInfoDto?> GetUserSubscriptionInfoAsync(Guid userId, CancellationToken ct = default)
    {
        var subscription = await dbContext.UserSubscriptions
            .Where(s => s.UserId == userId && ActiveStatuses.Contains(s.Status))
            .Include(s => s.Plan)
                .ThenInclude(p => p.Features)
            .FirstOrDefaultAsync(ct);

        if (subscription is null)
            return null;

        var features = subscription.Plan.Features
            .Select(f => f.Key)
            .ToList()
            .AsReadOnly();

        return new SubscriptionInfoDto(
            subscription.Plan.Name,
            features,
            subscription.Status.ToString(),
            subscription.TrialEnd);
    }
}
