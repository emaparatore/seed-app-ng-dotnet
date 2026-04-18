using Seed.Application.Common.Models;

namespace Seed.Application.Common.Interfaces;

public interface IPaymentGateway
{
    Task<string> CreateCustomerAsync(string email, string name, CancellationToken ct = default);
    Task<string> CreateCheckoutSessionAsync(CreateCheckoutRequest request, CancellationToken ct = default);
    Task<string> CreateCustomerPortalSessionAsync(string stripeCustomerId, string returnUrl, CancellationToken ct = default);
    Task CancelSubscriptionAsync(string stripeSubscriptionId, CancellationToken ct = default);
    Task<SubscriptionDetails?> GetSubscriptionAsync(string stripeSubscriptionId, CancellationToken ct = default);
    Task<SubscriptionDetails> UpdateSubscriptionPriceAsync(string stripeSubscriptionId, string newPriceId, CancellationToken ct = default);
    Task<string> CreateUpgradePortalSessionAsync(string stripeCustomerId, string stripeSubscriptionId, string newPriceId, string returnUrl, CancellationToken ct = default);
    Task<ScheduledDowngradeResult> ScheduleSubscriptionDowngradeAsync(string stripeSubscriptionId, string newPriceId, CancellationToken ct = default);
    Task CancelSubscriptionScheduleAsync(string scheduleId, CancellationToken ct = default);
    Task<ProductSyncResult> SyncPlanToProviderAsync(SyncPlanRequest request, CancellationToken ct = default);
    Task DeleteCustomerAsync(string stripeCustomerId, CancellationToken ct = default);
}
