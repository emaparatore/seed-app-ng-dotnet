using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Seed.Application.Common.Interfaces;
using Seed.Domain.Authorization;
using Seed.Domain.Entities;
using Seed.Domain.Enums;
using Seed.Infrastructure.Persistence;
using Stripe;
using System.Globalization;
using System.Text.Json;

namespace Seed.Infrastructure.Services.Payments;

public sealed class StripeWebhookEventHandler(
    ApplicationDbContext dbContext,
    IAuditService auditService,
    IMemoryCache cache,
    IEmailService emailService,
    ILogger<StripeWebhookEventHandler> logger) : IWebhookEventHandler
{
    private static readonly TimeSpan IdempotencyCacheDuration = TimeSpan.FromHours(24);

    public async Task<bool> ProcessEventAsync(string eventId, string eventType, string jsonPayload, CancellationToken ct = default)
    {
        var cacheKey = $"webhook:{eventId}";
        if (cache.TryGetValue(cacheKey, out _))
        {
            logger.LogInformation("Duplicate webhook event {EventId} skipped", eventId);
            return true;
        }

        var processed = eventType switch
        {
            EventTypes.CheckoutSessionCompleted => await HandleCheckoutSessionCompletedAsync(jsonPayload, ct),
            EventTypes.InvoicePaymentSucceeded => await HandleInvoicePaymentSucceededAsync(jsonPayload, ct),
            EventTypes.InvoicePaymentFailed => await HandleInvoicePaymentFailedAsync(jsonPayload, ct),
            EventTypes.CustomerSubscriptionUpdated => await HandleSubscriptionUpdatedAsync(jsonPayload, ct),
            EventTypes.CustomerSubscriptionDeleted => await HandleSubscriptionDeletedAsync(jsonPayload, ct),
            EventTypes.CustomerSubscriptionTrialWillEnd => await HandleTrialWillEndAsync(jsonPayload, ct),
            _ => false,
        };

        if (processed)
        {
            cache.Set(cacheKey, true, IdempotencyCacheDuration);
        }

        return processed;
    }

    private async Task<bool> HandleCheckoutSessionCompletedAsync(string jsonPayload, CancellationToken ct)
    {
        var stripeEvent = EventUtility.ParseEvent(jsonPayload);
        var session = stripeEvent.Data.Object as Stripe.Checkout.Session;
        if (session is null) return false;

        var userId = session.Metadata?.GetValueOrDefault("userId");
        var planId = session.Metadata?.GetValueOrDefault("planId");

        if (string.IsNullOrWhiteSpace(userId) || !Guid.TryParse(userId, out var userGuid))
        {
            logger.LogWarning("checkout.session.completed missing or invalid userId metadata");
            return false;
        }

        Guid? planGuid = null;
        if (!string.IsNullOrWhiteSpace(planId) && Guid.TryParse(planId, out var parsedPlanGuid))
        {
            planGuid = parsedPlanGuid;
        }

        // If no planId in metadata, try to find plan by Stripe price ID
        if (planGuid is null && session.LineItems?.Data?.Count > 0)
        {
            var priceId = session.LineItems.Data[0].Price?.Id;
            if (!string.IsNullOrWhiteSpace(priceId))
            {
                var plan = await dbContext.SubscriptionPlans
                    .FirstOrDefaultAsync(p => p.StripePriceIdMonthly == priceId || p.StripePriceIdYearly == priceId, ct);
                planGuid = plan?.Id;
            }
        }

        if (planGuid is null)
        {
            logger.LogWarning("checkout.session.completed could not resolve plan for session {SessionId}", session.Id);
            return false;
        }

        var subscriptionId = session.SubscriptionId ?? session.Subscription?.Id;
        var customerId = session.CustomerId ?? session.Customer?.Id;

        var status = session.Status == "complete"
            ? (session.SubscriptionId is not null ? SubscriptionStatus.Active : SubscriptionStatus.Active)
            : SubscriptionStatus.Active;

        // Deactivate any existing active subscriptions for this user (safety net for plan changes)
        var existingSubscriptions = await dbContext.UserSubscriptions
            .Where(s => s.UserId == userGuid
                && (s.Status == SubscriptionStatus.Active || s.Status == SubscriptionStatus.Trialing)
                && s.StripeSubscriptionId != subscriptionId)
            .ToListAsync(ct);

        foreach (var existing in existingSubscriptions)
        {
            existing.Status = SubscriptionStatus.Canceled;
            existing.CanceledAt = DateTime.UtcNow;
            existing.UpdatedAt = DateTime.UtcNow;
            logger.LogInformation("Deactivated old subscription {OldSubscriptionId} for user {UserId}", existing.StripeSubscriptionId, userGuid);
        }

        var subscription = new UserSubscription
        {
            Id = Guid.NewGuid(),
            UserId = userGuid,
            PlanId = planGuid.Value,
            Status = status,
            StripeSubscriptionId = subscriptionId,
            StripeCustomerId = customerId,
            CurrentPeriodStart = DateTime.UtcNow,
            CurrentPeriodEnd = DateTime.UtcNow.AddMonths(1),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        dbContext.UserSubscriptions.Add(subscription);
        await dbContext.SaveChangesAsync(ct);

        await auditService.LogAsync(
            AuditActions.SubscriptionCreated,
            "UserSubscription",
            subscription.Id.ToString(),
            $"Plan: {planGuid}, StripeSubscription: {subscriptionId}",
            userGuid,
            cancellationToken: ct);

        logger.LogInformation("Subscription created for user {UserId}, plan {PlanId}", userGuid, planGuid);

        try
        {
            var user = await dbContext.Users.FindAsync([userGuid], ct);
            var plan = await dbContext.SubscriptionPlans.FindAsync([planGuid.Value], ct);
            if (user?.Email is not null && plan is not null)
                await emailService.SendSubscriptionConfirmationAsync(user.Email, plan.Name, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send subscription confirmation email for user {UserId}", userGuid);
        }

        return true;
    }

    private async Task<bool> HandleInvoicePaymentSucceededAsync(string jsonPayload, CancellationToken ct)
    {
        var stripeEvent = EventUtility.ParseEvent(jsonPayload);
        var invoice = stripeEvent.Data.Object as Invoice;
        if (invoice is null) return false;

        var stripeSubscriptionId = ResolveInvoiceSubscriptionId(invoice, jsonPayload);
        if (string.IsNullOrWhiteSpace(stripeSubscriptionId)) return false;

        var subscription = await dbContext.UserSubscriptions
            .FirstOrDefaultAsync(s => s.StripeSubscriptionId == stripeSubscriptionId, ct);

        if (subscription is null)
        {
            logger.LogWarning("invoice.payment_succeeded: subscription {StripeSubscriptionId} not found", stripeSubscriptionId);
            return false;
        }

        if (TryResolveInvoicePeriodStart(invoice, jsonPayload, out var periodStart))
            subscription.CurrentPeriodStart = periodStart;
        if (TryResolveInvoicePeriodEnd(invoice, jsonPayload, out var periodEnd))
            subscription.CurrentPeriodEnd = periodEnd;

        if (subscription.Status == SubscriptionStatus.PastDue)
            subscription.Status = SubscriptionStatus.Active;

        subscription.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(ct);

        await auditService.LogAsync(
            AuditActions.SubscriptionPaymentSucceeded,
            "UserSubscription",
            subscription.Id.ToString(),
            $"Invoice: {invoice.Id}",
            subscription.UserId,
            cancellationToken: ct);

        logger.LogInformation("Payment succeeded for subscription {SubscriptionId}", stripeSubscriptionId);
        return true;
    }

    private async Task<bool> HandleInvoicePaymentFailedAsync(string jsonPayload, CancellationToken ct)
    {
        var stripeEvent = EventUtility.ParseEvent(jsonPayload);
        var invoice = stripeEvent.Data.Object as Invoice;
        if (invoice is null) return false;

        var stripeSubscriptionId = ResolveInvoiceSubscriptionId(invoice, jsonPayload);
        if (string.IsNullOrWhiteSpace(stripeSubscriptionId)) return false;

        var subscription = await dbContext.UserSubscriptions
            .FirstOrDefaultAsync(s => s.StripeSubscriptionId == stripeSubscriptionId, ct);

        if (subscription is null)
        {
            logger.LogWarning("invoice.payment_failed: subscription {StripeSubscriptionId} not found", stripeSubscriptionId);
            return false;
        }

        subscription.Status = SubscriptionStatus.PastDue;
        subscription.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(ct);

        await auditService.LogAsync(
            AuditActions.SubscriptionPaymentFailed,
            "UserSubscription",
            subscription.Id.ToString(),
            $"Invoice: {invoice.Id}",
            subscription.UserId,
            cancellationToken: ct);

        logger.LogWarning("Payment failed for subscription {SubscriptionId}", stripeSubscriptionId);
        return true;
    }

    private static string? ResolveInvoiceSubscriptionId(Invoice invoice, string jsonPayload)
    {
        if (!string.IsNullOrWhiteSpace(invoice.Parent?.SubscriptionDetails?.SubscriptionId))
            return invoice.Parent.SubscriptionDetails.SubscriptionId;

        return TryReadInvoiceStringField(jsonPayload, "subscription")
            ?? TryReadInvoiceStringField(jsonPayload, "subscription_id")
            ?? TryReadInvoiceParentSubscriptionDetailsId(jsonPayload);
    }

    private static bool TryResolveInvoicePeriodStart(Invoice invoice, string jsonPayload, out DateTime periodStart)
    {
        if (invoice.PeriodStart != default)
        {
            periodStart = invoice.PeriodStart;
            return true;
        }

        return TryReadInvoiceUnixField(jsonPayload, "period_start", out periodStart);
    }

    private static bool TryResolveInvoicePeriodEnd(Invoice invoice, string jsonPayload, out DateTime periodEnd)
    {
        if (invoice.PeriodEnd != default)
        {
            periodEnd = invoice.PeriodEnd;
            return true;
        }

        return TryReadInvoiceUnixField(jsonPayload, "period_end", out periodEnd);
    }

    private static string? TryReadInvoiceStringField(string jsonPayload, string fieldName)
    {
        if (!TryGetInvoiceJsonObject(jsonPayload, out var invoiceObject))
            return null;

        if (!invoiceObject.TryGetProperty(fieldName, out var value))
            return null;

        return value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static string? TryReadInvoiceParentSubscriptionDetailsId(string jsonPayload)
    {
        if (!TryGetInvoiceJsonObject(jsonPayload, out var invoiceObject))
            return null;

        if (!invoiceObject.TryGetProperty("parent", out var parentObject) || parentObject.ValueKind != JsonValueKind.Object)
            return null;

        if (!parentObject.TryGetProperty("subscription_details", out var subscriptionDetailsObject) || subscriptionDetailsObject.ValueKind != JsonValueKind.Object)
            return null;

        if (!subscriptionDetailsObject.TryGetProperty("subscription", out var subscriptionValue) || subscriptionValue.ValueKind != JsonValueKind.String)
            return null;

        return subscriptionValue.GetString();
    }

    private static bool TryReadInvoiceUnixField(string jsonPayload, string fieldName, out DateTime value)
    {
        value = default;
        if (!TryGetInvoiceJsonObject(jsonPayload, out var invoiceObject))
            return false;

        if (!invoiceObject.TryGetProperty(fieldName, out var field))
            return false;

        long unixValue;
        switch (field.ValueKind)
        {
            case JsonValueKind.Number when field.TryGetInt64(out var numberValue):
                unixValue = numberValue;
                break;
            case JsonValueKind.String when long.TryParse(field.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var stringValue):
                unixValue = stringValue;
                break;
            default:
                return false;
        }

        value = DateTimeOffset.FromUnixTimeSeconds(unixValue).UtcDateTime;
        return true;
    }

    private static bool TryGetInvoiceJsonObject(string jsonPayload, out JsonElement invoiceObject)
    {
        invoiceObject = default;
        try
        {
            using var document = JsonDocument.Parse(jsonPayload);
            if (!document.RootElement.TryGetProperty("data", out var dataObject) || dataObject.ValueKind != JsonValueKind.Object)
                return false;

            if (!dataObject.TryGetProperty("object", out var payloadObject) || payloadObject.ValueKind != JsonValueKind.Object)
                return false;

            invoiceObject = payloadObject.Clone();
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private async Task<bool> HandleSubscriptionUpdatedAsync(string jsonPayload, CancellationToken ct)
    {
        var stripeEvent = EventUtility.ParseEvent(jsonPayload);
        var stripeSub = stripeEvent.Data.Object as Subscription;
        if (stripeSub is null) return false;

        var subscription = await dbContext.UserSubscriptions
            .FirstOrDefaultAsync(s => s.StripeSubscriptionId == stripeSub.Id, ct);

        if (subscription is null)
        {
            logger.LogWarning("customer.subscription.updated: subscription {StripeSubscriptionId} not found", stripeSub.Id);
            return false;
        }

        var firstItem = stripeSub.Items?.Data?.FirstOrDefault();
        subscription.Status = MapStripeStatus(stripeSub.Status);
        subscription.CurrentPeriodStart = firstItem?.CurrentPeriodStart ?? subscription.CurrentPeriodStart;
        subscription.CurrentPeriodEnd = firstItem?.CurrentPeriodEnd ?? subscription.CurrentPeriodEnd;
        subscription.TrialEnd = stripeSub.TrialEnd;

        // Check if plan changed via price ID
        var newPriceId = stripeSub.Items?.Data?.FirstOrDefault()?.Price?.Id;
        if (!string.IsNullOrWhiteSpace(newPriceId))
        {
            var newPlan = await dbContext.SubscriptionPlans
                .FirstOrDefaultAsync(p => p.StripePriceIdMonthly == newPriceId || p.StripePriceIdYearly == newPriceId, ct);
            if (newPlan is not null && newPlan.Id != subscription.PlanId)
            {
                subscription.PlanId = newPlan.Id;

                // Clear scheduled downgrade fields — the plan change has been applied
                if (subscription.ScheduledPlanId is not null)
                {
                    subscription.ScheduledPlanId = null;
                    subscription.StripeScheduleId = null;
                }
            }
        }

        subscription.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(ct);

        await auditService.LogAsync(
            AuditActions.SubscriptionUpdated,
            "UserSubscription",
            subscription.Id.ToString(),
            $"Status: {subscription.Status}, Period: {subscription.CurrentPeriodStart:u} - {subscription.CurrentPeriodEnd:u}",
            subscription.UserId,
            cancellationToken: ct);

        logger.LogInformation("Subscription {SubscriptionId} updated, status: {Status}", stripeSub.Id, subscription.Status);
        return true;
    }

    private async Task<bool> HandleSubscriptionDeletedAsync(string jsonPayload, CancellationToken ct)
    {
        var stripeEvent = EventUtility.ParseEvent(jsonPayload);
        var stripeSub = stripeEvent.Data.Object as Subscription;
        if (stripeSub is null) return false;

        var subscription = await dbContext.UserSubscriptions
            .FirstOrDefaultAsync(s => s.StripeSubscriptionId == stripeSub.Id, ct);

        if (subscription is null)
        {
            logger.LogWarning("customer.subscription.deleted: subscription {StripeSubscriptionId} not found", stripeSub.Id);
            return false;
        }

        subscription.Status = SubscriptionStatus.Canceled;
        subscription.CanceledAt = DateTime.UtcNow;
        subscription.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(ct);

        await auditService.LogAsync(
            AuditActions.SubscriptionCanceled,
            "UserSubscription",
            subscription.Id.ToString(),
            $"Canceled at: {subscription.CanceledAt:u}",
            subscription.UserId,
            cancellationToken: ct);

        logger.LogInformation("Subscription {SubscriptionId} canceled", stripeSub.Id);

        try
        {
            var user = subscription.UserId.HasValue
                ? await dbContext.Users.FindAsync([subscription.UserId.Value], ct)
                : null;
            var plan = await dbContext.SubscriptionPlans.FindAsync([subscription.PlanId], ct);
            if (user?.Email is not null && plan is not null)
                await emailService.SendSubscriptionCanceledAsync(user.Email, plan.Name, subscription.CurrentPeriodEnd, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send subscription canceled email for subscription {SubscriptionId}", stripeSub.Id);
        }

        return true;
    }

    private async Task<bool> HandleTrialWillEndAsync(string jsonPayload, CancellationToken ct)
    {
        var stripeEvent = EventUtility.ParseEvent(jsonPayload);
        var stripeSub = stripeEvent.Data.Object as Subscription;
        if (stripeSub is null) return false;

        logger.LogInformation("Trial will end soon for subscription {SubscriptionId}", stripeSub.Id);

        try
        {
            var subscription = await dbContext.UserSubscriptions
                .FirstOrDefaultAsync(s => s.StripeSubscriptionId == stripeSub.Id, ct);

            if (subscription?.UserId is null) return true;

            var user = await dbContext.Users.FindAsync([subscription.UserId.Value], ct);
            var plan = await dbContext.SubscriptionPlans.FindAsync([subscription.PlanId], ct);

            if (user?.Email is not null && plan is not null)
            {
                var daysRemaining = stripeSub.TrialEnd.HasValue
                    ? Math.Max(0, (int)(stripeSub.TrialEnd.Value - DateTime.UtcNow).TotalDays)
                    : 3;
                await emailService.SendTrialEndingNotificationAsync(user.Email, plan.Name, daysRemaining, ct);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send trial ending notification for subscription {SubscriptionId}", stripeSub.Id);
        }

        return true;
    }

    internal static SubscriptionStatus MapStripeStatus(string stripeStatus)
    {
        return stripeStatus switch
        {
            "active" => SubscriptionStatus.Active,
            "trialing" => SubscriptionStatus.Trialing,
            "past_due" => SubscriptionStatus.PastDue,
            "canceled" => SubscriptionStatus.Canceled,
            "unpaid" => SubscriptionStatus.Expired,
            "incomplete_expired" => SubscriptionStatus.Expired,
            _ => SubscriptionStatus.Active,
        };
    }
}
