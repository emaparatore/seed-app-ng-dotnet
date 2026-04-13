using Seed.Application.Auth.Queries.GetCurrentUser;
using Seed.Application.Common.Interfaces;

namespace Seed.Infrastructure.Billing.Services;

public sealed class NullSubscriptionInfoService : ISubscriptionInfoService
{
    public Task<SubscriptionInfoDto?> GetUserSubscriptionInfoAsync(Guid userId, CancellationToken ct = default) =>
        Task.FromResult<SubscriptionInfoDto?>(null);
}
