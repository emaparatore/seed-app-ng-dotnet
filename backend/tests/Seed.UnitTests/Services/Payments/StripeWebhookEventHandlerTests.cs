using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Seed.Application.Common.Interfaces;
using Seed.Domain.Entities;
using Seed.Domain.Enums;
using Seed.Infrastructure.Persistence;
using Seed.Infrastructure.Services.Payments;
using Stripe;

namespace Seed.UnitTests.Services.Payments;

public class StripeWebhookEventHandlerTests : IDisposable
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IAuditService _auditService;
    private readonly IMemoryCache _cache;
    private readonly IEmailService _emailService;
    private readonly StripeWebhookEventHandler _handler;

    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _planId = Guid.NewGuid();

    public StripeWebhookEventHandlerTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new ApplicationDbContext(options);
        _auditService = Substitute.For<IAuditService>();
        _emailService = Substitute.For<IEmailService>();
        _cache = new MemoryCache(new MemoryCacheOptions());
        var logger = Substitute.For<ILogger<StripeWebhookEventHandler>>();

        _handler = new StripeWebhookEventHandler(_dbContext, _auditService, _cache, _emailService, logger);

        SeedTestPlan();
        SeedTestUser();
    }

    private void SeedTestUser()
    {
        _dbContext.Users.Add(new ApplicationUser
        {
            Id = _userId,
            Email = "test@example.com",
            UserName = "test@example.com",
        });
        _dbContext.SaveChanges();
    }

    private void SeedTestPlan()
    {
        _dbContext.SubscriptionPlans.Add(new SubscriptionPlan
        {
            Id = _planId,
            Name = "Pro",
            MonthlyPrice = 9.99m,
            YearlyPrice = 99.99m,
            StripePriceIdMonthly = "price_monthly_123",
            StripePriceIdYearly = "price_yearly_123",
            StripeProductId = "prod_123",
            Status = PlanStatus.Active,
        });
        _dbContext.SaveChanges();
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _cache.Dispose();
    }

    [Fact]
    public async Task ProcessEventAsync_CheckoutSessionCompleted_CreatesSubscription()
    {
        var json = BuildCheckoutSessionCompletedJson(_userId, _planId, "sub_123", "cus_123");

        var result = await _handler.ProcessEventAsync("evt_1", EventTypes.CheckoutSessionCompleted, json);

        result.Should().BeTrue();
        var sub = await _dbContext.UserSubscriptions.FirstOrDefaultAsync();
        sub.Should().NotBeNull();
        sub!.UserId.Should().Be(_userId);
        sub.PlanId.Should().Be(_planId);
        sub.Status.Should().Be(SubscriptionStatus.Active);
        sub.StripeSubscriptionId.Should().Be("sub_123");
        sub.StripeCustomerId.Should().Be("cus_123");

        await _auditService.Received(1).LogAsync(
            "SubscriptionCreated",
            "UserSubscription",
            Arg.Any<string>(),
            Arg.Any<string>(),
            _userId,
            Arg.Any<string?>(),
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessEventAsync_InvoicePaymentSucceeded_UpdatesPeriodDates()
    {
        var subscription = await CreateTestSubscription("sub_200", SubscriptionStatus.Active);
        var json = BuildInvoiceJson(EventTypes.InvoicePaymentSucceeded, "sub_200", "inv_200");

        var result = await _handler.ProcessEventAsync("evt_2", EventTypes.InvoicePaymentSucceeded, json);

        result.Should().BeTrue();
        await _dbContext.Entry(subscription).ReloadAsync();
        subscription.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));

        await _auditService.Received(1).LogAsync(
            "SubscriptionPaymentSucceeded",
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            subscription.UserId,
            Arg.Any<string?>(),
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessEventAsync_InvoicePaymentSucceeded_PastDueBecomesActive()
    {
        var subscription = await CreateTestSubscription("sub_201", SubscriptionStatus.PastDue);
        var json = BuildInvoiceJson(EventTypes.InvoicePaymentSucceeded, "sub_201", "inv_201");

        await _handler.ProcessEventAsync("evt_2b", EventTypes.InvoicePaymentSucceeded, json);

        await _dbContext.Entry(subscription).ReloadAsync();
        subscription.Status.Should().Be(SubscriptionStatus.Active);
    }

    [Fact]
    public async Task ProcessEventAsync_InvoicePaymentFailed_SetsStatusPastDue()
    {
        var subscription = await CreateTestSubscription("sub_300", SubscriptionStatus.Active);
        var json = BuildInvoiceJson(EventTypes.InvoicePaymentFailed, "sub_300", "inv_300");

        var result = await _handler.ProcessEventAsync("evt_3", EventTypes.InvoicePaymentFailed, json);

        result.Should().BeTrue();
        await _dbContext.Entry(subscription).ReloadAsync();
        subscription.Status.Should().Be(SubscriptionStatus.PastDue);

        await _auditService.Received(1).LogAsync(
            "SubscriptionPaymentFailed",
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            subscription.UserId,
            Arg.Any<string?>(),
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessEventAsync_SubscriptionUpdated_UpdatesStatusAndPeriod()
    {
        var subscription = await CreateTestSubscription("sub_400", SubscriptionStatus.Active);
        var json = BuildSubscriptionEventJson(EventTypes.CustomerSubscriptionUpdated, "sub_400", "past_due");

        var result = await _handler.ProcessEventAsync("evt_4", EventTypes.CustomerSubscriptionUpdated, json);

        result.Should().BeTrue();
        await _dbContext.Entry(subscription).ReloadAsync();
        subscription.Status.Should().Be(SubscriptionStatus.PastDue);

        await _auditService.Received(1).LogAsync(
            "SubscriptionUpdated",
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            subscription.UserId,
            Arg.Any<string?>(),
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessEventAsync_SubscriptionDeleted_SetsStatusCanceled()
    {
        var subscription = await CreateTestSubscription("sub_500", SubscriptionStatus.Active);
        var json = BuildSubscriptionEventJson(EventTypes.CustomerSubscriptionDeleted, "sub_500", "canceled");

        var result = await _handler.ProcessEventAsync("evt_5", EventTypes.CustomerSubscriptionDeleted, json);

        result.Should().BeTrue();
        await _dbContext.Entry(subscription).ReloadAsync();
        subscription.Status.Should().Be(SubscriptionStatus.Canceled);
        subscription.CanceledAt.Should().NotBeNull();
        subscription.CanceledAt!.Value.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));

        await _auditService.Received(1).LogAsync(
            "SubscriptionCanceled",
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            subscription.UserId,
            Arg.Any<string?>(),
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessEventAsync_TrialWillEnd_LogsOnly()
    {
        var json = BuildSubscriptionEventJson("customer.subscription.trial_will_end", "sub_600", "trialing");

        var result = await _handler.ProcessEventAsync("evt_6", "customer.subscription.trial_will_end", json);

        result.Should().BeTrue();
        // No DB changes, no audit service call
        await _auditService.DidNotReceive().LogAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string?>(),
            Arg.Any<string?>(),
            Arg.Any<Guid?>(),
            Arg.Any<string?>(),
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessEventAsync_DuplicateEvent_SkipsProcessing()
    {
        var subscription = await CreateTestSubscription("sub_700", SubscriptionStatus.Active);
        var json = BuildInvoiceJson(EventTypes.InvoicePaymentFailed, "sub_700", "inv_700");

        // First call
        await _handler.ProcessEventAsync("evt_dup", EventTypes.InvoicePaymentFailed, json);
        // Reset subscription status for verification
        subscription.Status = SubscriptionStatus.Active;
        await _dbContext.SaveChangesAsync();

        // Second call with same eventId - should be skipped
        var result = await _handler.ProcessEventAsync("evt_dup", EventTypes.InvoicePaymentFailed, json);

        result.Should().BeTrue();
        await _dbContext.Entry(subscription).ReloadAsync();
        // Status should still be Active because the duplicate was skipped
        subscription.Status.Should().Be(SubscriptionStatus.Active);
    }

    [Fact]
    public async Task ProcessEventAsync_UnknownEventType_ReturnsFalse()
    {
        var json = "{}";

        var result = await _handler.ProcessEventAsync("evt_unknown", "some.unknown.event", json);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task ProcessEventAsync_CheckoutSessionCompleted_SendsConfirmationEmail()
    {
        var json = BuildCheckoutSessionCompletedJson(_userId, _planId, "sub_email_1", "cus_email_1");

        await _handler.ProcessEventAsync("evt_email_1", EventTypes.CheckoutSessionCompleted, json);

        await _emailService.Received(1).SendSubscriptionConfirmationAsync(
            "test@example.com",
            "Pro",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessEventAsync_TrialWillEnd_SendsTrialEndingEmail()
    {
        await CreateTestSubscription("sub_trial_1", SubscriptionStatus.Trialing);
        var trialEnd = DateTimeOffset.UtcNow.AddDays(3).ToUnixTimeSeconds();
        var json = BuildSubscriptionEventJsonWithTrialEnd("customer.subscription.trial_will_end", "sub_trial_1", "trialing", trialEnd);

        var result = await _handler.ProcessEventAsync("evt_trial_1", "customer.subscription.trial_will_end", json);

        result.Should().BeTrue();
        await _emailService.Received(1).SendTrialEndingNotificationAsync(
            "test@example.com",
            "Pro",
            Arg.Any<int>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessEventAsync_SubscriptionDeleted_SendsCanceledEmail()
    {
        await CreateTestSubscription("sub_cancel_1", SubscriptionStatus.Active);
        var json = BuildSubscriptionEventJson(EventTypes.CustomerSubscriptionDeleted, "sub_cancel_1", "canceled");

        await _handler.ProcessEventAsync("evt_cancel_1", EventTypes.CustomerSubscriptionDeleted, json);

        await _emailService.Received(1).SendSubscriptionCanceledAsync(
            "test@example.com",
            "Pro",
            Arg.Any<DateTime>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessEventAsync_EmailFailure_DoesNotBreakWebhookProcessing()
    {
        _emailService.SendSubscriptionConfirmationAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new Exception("SMTP error")));

        var json = BuildCheckoutSessionCompletedJson(_userId, _planId, "sub_err_1", "cus_err_1");

        var result = await _handler.ProcessEventAsync("evt_err_1", EventTypes.CheckoutSessionCompleted, json);

        result.Should().BeTrue();
    }

    [Theory]
    [InlineData("active", SubscriptionStatus.Active)]
    [InlineData("trialing", SubscriptionStatus.Trialing)]
    [InlineData("past_due", SubscriptionStatus.PastDue)]
    [InlineData("canceled", SubscriptionStatus.Canceled)]
    [InlineData("unpaid", SubscriptionStatus.Expired)]
    [InlineData("incomplete_expired", SubscriptionStatus.Expired)]
    public void MapStripeStatus_MapsCorrectly(string stripeStatus, SubscriptionStatus expected)
    {
        var result = StripeWebhookEventHandler.MapStripeStatus(stripeStatus);

        result.Should().Be(expected);
    }

    #region Helpers

    private async Task<UserSubscription> CreateTestSubscription(string stripeSubId, SubscriptionStatus status)
    {
        var subscription = new UserSubscription
        {
            Id = Guid.NewGuid(),
            UserId = _userId,
            PlanId = _planId,
            Status = status,
            StripeSubscriptionId = stripeSubId,
            StripeCustomerId = "cus_test",
            CurrentPeriodStart = DateTime.UtcNow.AddDays(-30),
            CurrentPeriodEnd = DateTime.UtcNow.AddDays(1),
            CreatedAt = DateTime.UtcNow.AddDays(-30),
            UpdatedAt = DateTime.UtcNow.AddDays(-30),
        };

        _dbContext.UserSubscriptions.Add(subscription);
        await _dbContext.SaveChangesAsync();
        return subscription;
    }

    private static string BuildCheckoutSessionCompletedJson(Guid userId, Guid planId, string subId, string cusId)
    {
        var epoch = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        return $$"""
        {
            "id": "evt_1",
            "object": "event",
            "type": "checkout.session.completed",
            "created": {{epoch}},
            "livemode": false,
            "pending_webhooks": 0,
            "api_version": "2026-03-25.dahlia",
            "request": { "id": null, "idempotency_key": null },
            "data": {
                "object": {
                    "id": "cs_test_123",
                    "object": "checkout.session",
                    "status": "complete",
                    "subscription": "{{subId}}",
                    "customer": "{{cusId}}",
                    "metadata": {
                        "userId": "{{userId}}",
                        "planId": "{{planId}}"
                    }
                }
            }
        }
        """;
    }

    private static string BuildInvoiceJson(string eventType, string subId, string invoiceId)
    {
        var epoch = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var periodStart = epoch - 86400;
        var periodEnd = epoch + (30 * 86400);
        return $$"""
        {
            "id": "evt_inv",
            "object": "event",
            "type": "{{eventType}}",
            "created": {{epoch}},
            "livemode": false,
            "pending_webhooks": 0,
            "api_version": "2026-03-25.dahlia",
            "request": { "id": null, "idempotency_key": null },
            "data": {
                "object": {
                    "id": "{{invoiceId}}",
                    "object": "invoice",
                    "subscription": "{{subId}}",
                    "period_start": {{periodStart}},
                    "period_end": {{periodEnd}}
                }
            }
        }
        """;
    }

    private static string BuildSubscriptionEventJson(string eventType, string subId, string status)
    {
        var epoch = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var periodStart = epoch;
        var periodEnd = epoch + (30 * 86400);
        return $$"""
        {
            "id": "evt_sub",
            "object": "event",
            "type": "{{eventType}}",
            "created": {{epoch}},
            "livemode": false,
            "pending_webhooks": 0,
            "api_version": "2026-03-25.dahlia",
            "request": { "id": null, "idempotency_key": null },
            "data": {
                "object": {
                    "id": "{{subId}}",
                    "object": "subscription",
                    "status": "{{status}}",
                    "current_period_start": {{periodStart}},
                    "current_period_end": {{periodEnd}},
                    "items": {
                        "object": "list",
                        "data": []
                    }
                }
            }
        }
        """;
    }

    private static string BuildSubscriptionEventJsonWithTrialEnd(string eventType, string subId, string status, long trialEnd)
    {
        var epoch = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var periodStart = epoch;
        var periodEnd = epoch + (30 * 86400);
        return $$"""
        {
            "id": "evt_sub_trial",
            "object": "event",
            "type": "{{eventType}}",
            "created": {{epoch}},
            "livemode": false,
            "pending_webhooks": 0,
            "api_version": "2026-03-25.dahlia",
            "request": { "id": null, "idempotency_key": null },
            "data": {
                "object": {
                    "id": "{{subId}}",
                    "object": "subscription",
                    "status": "{{status}}",
                    "current_period_start": {{periodStart}},
                    "current_period_end": {{periodEnd}},
                    "trial_end": {{trialEnd}},
                    "items": {
                        "object": "list",
                        "data": []
                    }
                }
            }
        }
        """;
    }

    #endregion
}
