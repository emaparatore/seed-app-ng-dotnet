using Microsoft.Extensions.Logging;
using Seed.Application.Common.Interfaces;
using Seed.Application.Common.Models;

namespace Seed.Infrastructure.Services.Payments;

public sealed class MockPaymentGateway(ILogger<MockPaymentGateway> logger) : IPaymentGateway
{
    public Task<string> CreateCustomerAsync(string email, string name, CancellationToken ct = default)
    {
        var customerId = $"mock_cus_{Guid.NewGuid():N}";
        logger.LogWarning("MockPaymentGateway — CreateCustomer: {Email}, {Name} → {CustomerId}", email, name, customerId);
        return Task.FromResult(customerId);
    }

    public Task<string> CreateCheckoutSessionAsync(CreateCheckoutRequest request, CancellationToken ct = default)
    {
        var url = $"https://mock-checkout.example.com/session/{Guid.NewGuid():N}";
        logger.LogWarning("MockPaymentGateway — CreateCheckoutSession: PriceId={PriceId}, Email={Email} → {Url}",
            request.PriceId, request.CustomerEmail, url);
        return Task.FromResult(url);
    }

    public Task<string> CreateCustomerPortalSessionAsync(string stripeCustomerId, string returnUrl, CancellationToken ct = default)
    {
        var url = $"https://mock-portal.example.com/session/{Guid.NewGuid():N}";
        logger.LogWarning("MockPaymentGateway — CreateCustomerPortalSession: CustomerId={CustomerId} → {Url}",
            stripeCustomerId, url);
        return Task.FromResult(url);
    }

    public Task CancelSubscriptionAsync(string stripeSubscriptionId, CancellationToken ct = default)
    {
        logger.LogWarning("MockPaymentGateway — CancelSubscription: {SubscriptionId}", stripeSubscriptionId);
        return Task.CompletedTask;
    }

    public Task<SubscriptionDetails?> GetSubscriptionAsync(string stripeSubscriptionId, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var details = new SubscriptionDetails(
            SubscriptionId: stripeSubscriptionId,
            CustomerId: "mock_cus_default",
            Status: "active",
            PriceId: "mock_price_default",
            CurrentPeriodStart: now,
            CurrentPeriodEnd: now.AddDays(30),
            TrialEnd: null,
            CancelAtPeriodEnd: false);

        logger.LogWarning("MockPaymentGateway — GetSubscription: {SubscriptionId} → mock details", stripeSubscriptionId);
        return Task.FromResult<SubscriptionDetails?>(details);
    }

    public Task<SubscriptionDetails> UpdateSubscriptionPriceAsync(string stripeSubscriptionId, string newPriceId, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var details = new SubscriptionDetails(
            SubscriptionId: stripeSubscriptionId,
            CustomerId: "mock_cus_default",
            Status: "active",
            PriceId: newPriceId,
            CurrentPeriodStart: now,
            CurrentPeriodEnd: now.AddDays(30),
            TrialEnd: null,
            CancelAtPeriodEnd: false);

        logger.LogWarning("MockPaymentGateway — UpdateSubscriptionPrice: {SubscriptionId} → {PriceId}", stripeSubscriptionId, newPriceId);
        return Task.FromResult(details);
    }

    public Task<ScheduledDowngradeResult> ScheduleSubscriptionDowngradeAsync(string stripeSubscriptionId, string newPriceId, CancellationToken ct = default)
    {
        var result = new ScheduledDowngradeResult(
            ScheduleId: $"mock_sched_{Guid.NewGuid():N}",
            ScheduledDate: DateTime.UtcNow.AddDays(30));

        logger.LogWarning("MockPaymentGateway — ScheduleSubscriptionDowngrade: {SubscriptionId} → {PriceId} at {Date}",
            stripeSubscriptionId, newPriceId, result.ScheduledDate);
        return Task.FromResult(result);
    }

    public Task CancelSubscriptionScheduleAsync(string scheduleId, CancellationToken ct = default)
    {
        logger.LogWarning("MockPaymentGateway — CancelSubscriptionSchedule: {ScheduleId}", scheduleId);
        return Task.CompletedTask;
    }

    public Task DeleteCustomerAsync(string stripeCustomerId, CancellationToken ct = default)
    {
        logger.LogWarning("MockPaymentGateway — DeleteCustomer: {CustomerId}", stripeCustomerId);
        return Task.CompletedTask;
    }

    public Task<ProductSyncResult> SyncPlanToProviderAsync(SyncPlanRequest request, CancellationToken ct = default)
    {
        var result = new ProductSyncResult(
            ProductId: request.ProductId ?? $"mock_prod_{Guid.NewGuid():N}",
            MonthlyPriceId: request.ExistingMonthlyPriceId ?? $"mock_price_monthly_{Guid.NewGuid():N}",
            YearlyPriceId: request.ExistingYearlyPriceId ?? $"mock_price_yearly_{Guid.NewGuid():N}");

        logger.LogWarning("MockPaymentGateway — SyncPlanToProvider: {PlanName} → ProductId={ProductId}",
            request.Name, result.ProductId);
        return Task.FromResult(result);
    }
}
