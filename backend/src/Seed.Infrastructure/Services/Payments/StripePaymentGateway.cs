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

    public async Task<string> CreateCheckoutSessionAsync(CreateCheckoutRequest request, CancellationToken ct = default)
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
        return session.Url;
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
}
