using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Seed.Application.Common.Interfaces;
using Seed.Application.Common.Models;
using Seed.Shared.Configuration;
using Stripe;
using Stripe.Checkout;

namespace Seed.Infrastructure.Services.Payments;

public sealed class StripePaymentGateway(
    IOptions<StripeSettings> settings,
    ILogger<StripePaymentGateway> logger) : IPaymentGateway
{
    private readonly StripeClient _client = new(settings.Value.SecretKey);

    public async Task<string> CreateCustomerAsync(string email, string name, CancellationToken ct = default)
    {
        var service = new CustomerService(_client);
        var options = new CustomerCreateOptions
        {
            Email = email,
            Name = name,
        };

        var customer = await service.CreateAsync(options, cancellationToken: ct);
        logger.LogInformation("Stripe customer created: {CustomerId} for {Email}", customer.Id, email);
        return customer.Id;
    }

    public async Task<CheckoutSessionCreationResult> CreateCheckoutSessionAsync(CreateCheckoutRequest request, CancellationToken ct = default)
    {
        var service = new Stripe.Checkout.SessionService(_client);
        var options = new Stripe.Checkout.SessionCreateOptions
        {
            Mode = "subscription",
            SuccessUrl = request.SuccessUrl,
            CancelUrl = request.CancelUrl,
            LineItems =
            [
                new SessionLineItemOptions
                {
                    Price = request.PriceId,
                    Quantity = 1,
                },
            ],
            Metadata = request.Metadata,
        };

        if (!string.IsNullOrWhiteSpace(request.CustomerId))
        {
            options.Customer = request.CustomerId;
        }
        else
        {
            options.CustomerEmail = request.CustomerEmail;
        }

        if (request.TrialDays is > 0)
        {
            options.SubscriptionData = new SessionSubscriptionDataOptions
            {
                TrialPeriodDays = request.TrialDays,
            };
        }

        var session = await service.CreateAsync(options, cancellationToken: ct);
        logger.LogInformation("Stripe checkout session created: {SessionId}", session.Id);
        return new CheckoutSessionCreationResult(session.Id, session.Url ?? string.Empty);
    }

    public async Task<CheckoutSessionDetails?> GetCheckoutSessionAsync(string sessionId, CancellationToken ct = default)
    {
        var service = new Stripe.Checkout.SessionService(_client);

        try
        {
            var session = await service.GetAsync(sessionId, cancellationToken: ct);
            return new CheckoutSessionDetails(
                SessionId: session.Id,
                Status: session.Status ?? string.Empty,
                PaymentStatus: session.PaymentStatus ?? string.Empty,
                SubscriptionId: session.SubscriptionId,
                CustomerId: session.CustomerId,
                Metadata: session.Metadata ?? new Dictionary<string, string>());
        }
        catch (StripeException ex) when (ex.StripeError?.Type == "invalid_request_error"
                                         && ex.HttpStatusCode == System.Net.HttpStatusCode.NotFound)
        {
            logger.LogWarning("Stripe checkout session {SessionId} not found", sessionId);
            return null;
        }
    }

    public async Task<string> CreateCustomerPortalSessionAsync(string stripeCustomerId, string returnUrl, CancellationToken ct = default)
    {
        var service = new Stripe.BillingPortal.SessionService(_client);
        var options = new Stripe.BillingPortal.SessionCreateOptions
        {
            Customer = stripeCustomerId,
            ReturnUrl = returnUrl,
        };

        var session = await service.CreateAsync(options, cancellationToken: ct);
        logger.LogInformation("Stripe customer portal session created for {CustomerId}", stripeCustomerId);
        return session.Url;
    }

    public async Task CancelSubscriptionAsync(string stripeSubscriptionId, CancellationToken ct = default)
    {
        var service = new SubscriptionService(_client);
        var options = new SubscriptionUpdateOptions
        {
            CancelAtPeriodEnd = true,
        };

        await service.UpdateAsync(stripeSubscriptionId, options, cancellationToken: ct);
        logger.LogInformation("Stripe subscription {SubscriptionId} set to cancel at period end", stripeSubscriptionId);
    }

    public async Task<SubscriptionDetails?> GetSubscriptionAsync(string stripeSubscriptionId, CancellationToken ct = default)
    {
        var service = new SubscriptionService(_client);

        try
        {
            var subscription = await service.GetAsync(stripeSubscriptionId, cancellationToken: ct);
            return new SubscriptionDetails(
                SubscriptionId: subscription.Id,
                CustomerId: subscription.CustomerId,
                Status: subscription.Status,
                PriceId: subscription.Items.Data[0].Price.Id,
                CurrentPeriodStart: subscription.Items.Data[0].CurrentPeriodStart,
                CurrentPeriodEnd: subscription.Items.Data[0].CurrentPeriodEnd,
                TrialEnd: subscription.TrialEnd,
                CancelAtPeriodEnd: subscription.CancelAtPeriodEnd);
        }
        catch (StripeException ex) when (ex.StripeError?.Type == "invalid_request_error"
                                         && ex.HttpStatusCode == System.Net.HttpStatusCode.NotFound)
        {
            logger.LogWarning("Stripe subscription {SubscriptionId} not found", stripeSubscriptionId);
            return null;
        }
    }

    public async Task<InvoicePaymentDetails?> GetLatestPaidInvoiceAsync(string stripeSubscriptionId, CancellationToken ct = default)
    {
        var service = new InvoiceService(_client);
        var options = new InvoiceListOptions
        {
            Subscription = stripeSubscriptionId,
            Limit = 20,
            Expand = ["data.lines.data"],
        };

        var invoices = await service.ListAsync(options, cancellationToken: ct);
        var invoice = invoices.Data
            .Where(i => string.Equals(i.Status, "paid", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(i => i.Created)
            .FirstOrDefault();

        if (invoice is null)
            return null;

        var subtotal = ToDecimal(invoice.Subtotal);
        var total = ToDecimal(invoice.Total);
        var amountPaid = ToDecimal(invoice.AmountPaid);
        var taxInCents = TryGetLongProperty(invoice, "Tax") ?? Math.Max(0, invoice.Total - invoice.Subtotal);
        var amountTax = ToDecimal(taxInCents);

        var prorationInCents = invoice.Lines?.Data?
            .Where(l => TryGetBoolProperty(l, "Proration") == true)
            .Sum(l => l.Amount) ?? 0;

        var paymentIntentId = TryGetStringProperty(invoice, "PaymentIntentId")
            ?? TryGetNestedIdProperty(invoice, "PaymentIntent");

        return new InvoicePaymentDetails(
            StripeInvoiceId: invoice.Id,
            StripePaymentIntentId: paymentIntentId,
            InvoicePeriodStart: invoice.PeriodStart == default ? null : invoice.PeriodStart,
            InvoicePeriodEnd: invoice.PeriodEnd == default ? null : invoice.PeriodEnd,
            Currency: invoice.Currency?.ToUpperInvariant(),
            AmountSubtotal: subtotal,
            AmountTax: amountTax,
            AmountTotal: total,
            AmountPaid: amountPaid,
            IsProrationApplied: prorationInCents != 0,
            ProrationAmount: ToDecimal(prorationInCents),
            BillingReason: invoice.BillingReason);
    }

    public async Task<ProductSyncResult> SyncPlanToProviderAsync(SyncPlanRequest request, CancellationToken ct = default)
    {
        var productService = new ProductService(_client);
        string productId;

        if (string.IsNullOrWhiteSpace(request.ProductId))
        {
            var product = await productService.CreateAsync(new ProductCreateOptions
            {
                Name = request.Name,
                Description = request.Description,
            }, cancellationToken: ct);
            productId = product.Id;
            logger.LogInformation("Stripe product created: {ProductId}", productId);
        }
        else
        {
            await productService.UpdateAsync(request.ProductId, new ProductUpdateOptions
            {
                Name = request.Name,
                Description = request.Description,
            }, cancellationToken: ct);
            productId = request.ProductId;
            logger.LogInformation("Stripe product updated: {ProductId}", productId);
        }

        var priceService = new PriceService(_client);

        var monthlyPriceId = await CreatePriceIfNeededAsync(
            priceService, productId, request.MonthlyPriceInCents, "month",
            request.ExistingMonthlyPriceId, ct);

        var yearlyPriceId = await CreatePriceIfNeededAsync(
            priceService, productId, request.YearlyPriceInCents, "year",
            request.ExistingYearlyPriceId, ct);

        return new ProductSyncResult(productId, monthlyPriceId, yearlyPriceId);
    }

    public async Task DeleteCustomerAsync(string stripeCustomerId, CancellationToken ct = default)
    {
        var service = new CustomerService(_client);
        await service.DeleteAsync(stripeCustomerId, cancellationToken: ct);
        logger.LogInformation("Stripe customer deleted: {CustomerId}", stripeCustomerId);
    }

    private async Task<string> CreatePriceIfNeededAsync(
        PriceService priceService,
        string productId,
        long amountInCents,
        string interval,
        string? existingPriceId,
        CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(existingPriceId))
        {
            var existingPrice = await priceService.GetAsync(existingPriceId, cancellationToken: ct);
            if (existingPrice.UnitAmount == amountInCents)
            {
                return existingPriceId;
            }
        }

        var price = await priceService.CreateAsync(new PriceCreateOptions
        {
            Product = productId,
            UnitAmount = amountInCents,
            Currency = "eur",
            Recurring = new PriceRecurringOptions
            {
                Interval = interval,
            },
        }, cancellationToken: ct);

        logger.LogInformation("Stripe {Interval} price created: {PriceId} ({Amount} cents)",
            interval, price.Id, amountInCents);
        return price.Id;
    }

    private static decimal ToDecimal(long amountInCents)
    {
        return amountInCents / 100m;
    }

    private static long? TryGetLongProperty(object source, string propertyName)
    {
        var property = source.GetType().GetProperty(propertyName);
        if (property is null)
            return null;

        var value = property.GetValue(source);
        if (value is long longValue)
            return longValue;
        if (value is int intValue)
            return intValue;

        return null;
    }

    private static bool? TryGetBoolProperty(object source, string propertyName)
    {
        var property = source.GetType().GetProperty(propertyName);
        if (property is null)
            return null;

        var value = property.GetValue(source);
        if (value is bool boolValue)
            return boolValue;

        return null;
    }

    private static string? TryGetStringProperty(object source, string propertyName)
    {
        var property = source.GetType().GetProperty(propertyName);
        if (property is null)
            return null;

        return property.GetValue(source) as string;
    }

    private static string? TryGetNestedIdProperty(object source, string propertyName)
    {
        var property = source.GetType().GetProperty(propertyName);
        if (property is null)
            return null;

        var nested = property.GetValue(source);
        if (nested is null)
            return null;

        var idProperty = nested.GetType().GetProperty("Id");
        return idProperty?.GetValue(nested) as string;
    }
}
