using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Seed.Application.Billing.Commands.CreateCheckoutSession;
using Seed.Application.Common.Interfaces;
using Seed.Application.Common.Models;
using Seed.Domain.Entities;
using Seed.Domain.Enums;
using Seed.Infrastructure.Billing.Commands;
using Seed.Infrastructure.Persistence;

namespace Seed.UnitTests.Billing.Commands;

public class CreateCheckoutSessionCommandHandlerTests : IDisposable
{
    private readonly ApplicationDbContext _dbContext;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IPaymentGateway _paymentGateway;
    private readonly IAuditService _auditService;
    private readonly CreateCheckoutSessionCommandHandler _handler;

    private static readonly Guid TestUserId = Guid.NewGuid();
    private static readonly ApplicationUser TestUser = new()
    {
        Id = TestUserId,
        Email = "test@example.com",
        FirstName = "Test",
        LastName = "User"
    };

    public CreateCheckoutSessionCommandHandlerTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new ApplicationDbContext(options);

        var store = Substitute.For<IUserStore<ApplicationUser>>();
        _userManager = Substitute.For<UserManager<ApplicationUser>>(
            store, null, null, null, null, null, null, null, null);
        _paymentGateway = Substitute.For<IPaymentGateway>();
        _auditService = Substitute.For<IAuditService>();

        _userManager.FindByIdAsync(TestUserId.ToString()).Returns(TestUser);
        _paymentGateway.CreateCheckoutSessionAsync(Arg.Any<CreateCheckoutRequest>(), Arg.Any<CancellationToken>())
            .Returns("https://checkout.stripe.com/test-session");
        _paymentGateway.CreateCustomerAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("cus_new_123");

        _handler = new CreateCheckoutSessionCommandHandler(_dbContext, _userManager, _paymentGateway, _auditService);
    }

    private CreateCheckoutSessionCommand CreateCommand(Guid? planId = null) => new(
        planId ?? Guid.NewGuid(),
        BillingInterval.Monthly,
        "https://example.com/success",
        "https://example.com/cancel")
    { UserId = TestUserId };

    private SubscriptionPlan CreatePlan(
        PlanStatus status = PlanStatus.Active,
        bool isFreeTier = false,
        string? monthlyPriceId = "price_monthly_123",
        string? yearlyPriceId = "price_yearly_123",
        int trialDays = 0)
    {
        return new SubscriptionPlan
        {
            Id = Guid.NewGuid(),
            Name = "Pro Plan",
            MonthlyPrice = 9.99m,
            YearlyPrice = 99.99m,
            StripePriceIdMonthly = monthlyPriceId,
            StripePriceIdYearly = yearlyPriceId,
            Status = status,
            IsFreeTier = isFreeTier,
            TrialDays = trialDays
        };
    }

    [Fact]
    public async Task Should_Return_CheckoutUrl_For_Active_Plan_With_Monthly_Price()
    {
        var plan = CreatePlan();
        _dbContext.SubscriptionPlans.Add(plan);
        await _dbContext.SaveChangesAsync();

        var result = await _handler.Handle(CreateCommand(plan.Id), CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.Data!.CheckoutUrl.Should().Be("https://checkout.stripe.com/test-session");
    }

    [Fact]
    public async Task Should_Use_Yearly_PriceId_For_Yearly_Interval()
    {
        var plan = CreatePlan();
        _dbContext.SubscriptionPlans.Add(plan);
        await _dbContext.SaveChangesAsync();

        var command = new CreateCheckoutSessionCommand(
            plan.Id, BillingInterval.Yearly, "https://example.com/success", "https://example.com/cancel")
        { UserId = TestUserId };

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        await _paymentGateway.Received(1).CreateCheckoutSessionAsync(
            Arg.Is<CreateCheckoutRequest>(r => r.PriceId == "price_yearly_123"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_Fail_When_Plan_Not_Found()
    {
        var result = await _handler.Handle(CreateCommand(), CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("Plan not found.");
    }

    [Fact]
    public async Task Should_Fail_When_Plan_Is_Not_Active()
    {
        var plan = CreatePlan(status: PlanStatus.Archived);
        _dbContext.SubscriptionPlans.Add(plan);
        await _dbContext.SaveChangesAsync();

        var result = await _handler.Handle(CreateCommand(plan.Id), CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("Plan is not active.");
    }

    [Fact]
    public async Task Should_Fail_When_Plan_Is_FreeTier()
    {
        var plan = CreatePlan(isFreeTier: true);
        _dbContext.SubscriptionPlans.Add(plan);
        await _dbContext.SaveChangesAsync();

        var result = await _handler.Handle(CreateCommand(plan.Id), CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("Cannot create a checkout session for a free tier plan.");
    }

    [Fact]
    public async Task Should_Fail_When_PriceId_Is_Null_For_Chosen_Interval()
    {
        var plan = CreatePlan(monthlyPriceId: null);
        _dbContext.SubscriptionPlans.Add(plan);
        await _dbContext.SaveChangesAsync();

        var result = await _handler.Handle(CreateCommand(plan.Id), CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("No price configured for Monthly billing interval.");
    }

    [Fact]
    public async Task Should_Create_Customer_When_No_Existing_StripeCustomerId()
    {
        var plan = CreatePlan();
        _dbContext.SubscriptionPlans.Add(plan);
        await _dbContext.SaveChangesAsync();

        var result = await _handler.Handle(CreateCommand(plan.Id), CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        await _paymentGateway.Received(1).CreateCustomerAsync(
            "test@example.com", "Test User", Arg.Any<CancellationToken>());
        await _paymentGateway.Received(1).CreateCheckoutSessionAsync(
            Arg.Is<CreateCheckoutRequest>(r => r.CustomerId == "cus_new_123"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_Reuse_Existing_StripeCustomerId()
    {
        var plan = CreatePlan();
        _dbContext.SubscriptionPlans.Add(plan);
        _dbContext.UserSubscriptions.Add(new UserSubscription
        {
            Id = Guid.NewGuid(),
            UserId = TestUserId,
            PlanId = plan.Id,
            StripeCustomerId = "cus_existing_456",
            CurrentPeriodStart = DateTime.UtcNow,
            CurrentPeriodEnd = DateTime.UtcNow.AddMonths(1)
        });
        await _dbContext.SaveChangesAsync();

        var result = await _handler.Handle(CreateCommand(plan.Id), CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        await _paymentGateway.DidNotReceive().CreateCustomerAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _paymentGateway.Received(1).CreateCheckoutSessionAsync(
            Arg.Is<CreateCheckoutRequest>(r => r.CustomerId == "cus_existing_456"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_Include_TrialDays_When_Plan_Has_Trial()
    {
        var plan = CreatePlan(trialDays: 14);
        _dbContext.SubscriptionPlans.Add(plan);
        await _dbContext.SaveChangesAsync();

        var result = await _handler.Handle(CreateCommand(plan.Id), CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        await _paymentGateway.Received(1).CreateCheckoutSessionAsync(
            Arg.Is<CreateCheckoutRequest>(r => r.TrialDays == 14),
            Arg.Any<CancellationToken>());
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
