using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Seed.Application.Common.Models;
using Seed.Infrastructure.Services.Payments;

namespace Seed.UnitTests.Services;

public class MockPaymentGatewayTests
{
    private readonly MockPaymentGateway _gateway;

    public MockPaymentGatewayTests()
    {
        var logger = Substitute.For<ILogger<MockPaymentGateway>>();
        _gateway = new MockPaymentGateway(logger);
    }

    [Fact]
    public async Task CreateCustomerAsync_ReturnsCustomerIdStartingWithMockPrefix()
    {
        var result = await _gateway.CreateCustomerAsync("test@example.com", "Test User");

        result.Should().StartWith("mock_cus_");
    }

    [Fact]
    public async Task CreateCheckoutSessionAsync_ReturnsUrl()
    {
        var request = new CreateCheckoutRequest(
            PriceId: "price_123",
            CustomerEmail: "test@example.com",
            CustomerId: null,
            SuccessUrl: "https://example.com/success",
            CancelUrl: "https://example.com/cancel",
            TrialDays: null,
            Metadata: null);

        var result = await _gateway.CreateCheckoutSessionAsync(request);

        result.SessionId.Should().StartWith("mock_cs_");
        result.CheckoutUrl.Should().StartWith("https://mock-checkout.example.com/session/");
    }

    [Fact]
    public async Task CreateCustomerPortalSessionAsync_ReturnsUrl()
    {
        var result = await _gateway.CreateCustomerPortalSessionAsync("cus_123", "https://example.com/return");

        result.Should().StartWith("https://mock-portal.example.com/session/");
    }

    [Fact]
    public async Task CancelSubscriptionAsync_CompletesSuccessfully()
    {
        var act = () => _gateway.CancelSubscriptionAsync("sub_123");

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task GetSubscriptionAsync_ReturnsDetails()
    {
        var result = await _gateway.GetSubscriptionAsync("sub_123");

        result.Should().NotBeNull();
        result!.SubscriptionId.Should().Be("sub_123");
        result.CustomerId.Should().Be("mock_cus_default");
        result.Status.Should().Be("active");
        result.PriceId.Should().Be("mock_price_default");
        result.CancelAtPeriodEnd.Should().BeFalse();
        result.CurrentPeriodEnd.Should().BeAfter(result.CurrentPeriodStart);
    }

    [Fact]
    public async Task SyncPlanToProviderAsync_ReturnsProductSyncResult()
    {
        var request = new SyncPlanRequest(
            ProductId: null,
            Name: "Pro Plan",
            Description: "Pro features",
            MonthlyPriceInCents: 999,
            YearlyPriceInCents: 9990,
            ExistingMonthlyPriceId: null,
            ExistingYearlyPriceId: null);

        var result = await _gateway.SyncPlanToProviderAsync(request);

        result.Should().NotBeNull();
        result.ProductId.Should().StartWith("mock_prod_");
        result.MonthlyPriceId.Should().StartWith("mock_price_monthly_");
        result.YearlyPriceId.Should().StartWith("mock_price_yearly_");
    }
}
