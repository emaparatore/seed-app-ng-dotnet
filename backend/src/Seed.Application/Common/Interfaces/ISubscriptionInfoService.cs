using Seed.Application.Auth.Queries.GetCurrentUser;

namespace Seed.Application.Common.Interfaces;

public interface ISubscriptionInfoService
{
    Task<SubscriptionInfoDto?> GetUserSubscriptionInfoAsync(Guid userId, CancellationToken ct = default);
}
