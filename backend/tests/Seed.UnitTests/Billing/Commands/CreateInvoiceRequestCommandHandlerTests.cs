using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Seed.Application.Billing.Commands.CreateInvoiceRequest;
using Seed.Application.Common.Interfaces;
using Seed.Application.Common.Models;
using Seed.Domain.Authorization;
using Seed.Domain.Entities;
using Seed.Domain.Enums;
using Seed.Infrastructure.Billing.Commands;
using Seed.Infrastructure.Persistence;

namespace Seed.UnitTests.Billing.Commands;

public class CreateInvoiceRequestCommandHandlerTests : IDisposable
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IAuditService _auditService;
    private readonly IPaymentGateway _paymentGateway;
    private readonly CreateInvoiceRequestCommandHandler _handler;

    private static readonly Guid TestUserId = Guid.NewGuid();
    private readonly Guid _subscriptionId;

    public CreateInvoiceRequestCommandHandlerTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new ApplicationDbContext(options);
        _auditService = Substitute.For<IAuditService>();
        _paymentGateway = Substitute.For<IPaymentGateway>();
        _handler = new CreateInvoiceRequestCommandHandler(_dbContext, _paymentGateway, _auditService);

        var planId = Guid.NewGuid();
        _subscriptionId = Guid.NewGuid();

        _dbContext.SubscriptionPlans.Add(new SubscriptionPlan
        {
            Id = planId,
            Name = "Pro",
            MonthlyPrice = 19,
            YearlyPrice = 190,
            TrialDays = 0,
            IsFreeTier = false,
            IsDefault = false,
            IsPopular = false,
            Status = PlanStatus.Active,
            SortOrder = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        _dbContext.UserSubscriptions.Add(new UserSubscription
        {
            Id = _subscriptionId,
            UserId = TestUserId,
            PlanId = planId,
            Status = SubscriptionStatus.Active,
            StripeSubscriptionId = "sub_test_123",
            CurrentPeriodStart = DateTime.UtcNow.AddDays(-5),
            CurrentPeriodEnd = DateTime.UtcNow.AddDays(25),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        _dbContext.SaveChanges();

        _paymentGateway
            .GetLatestPaidInvoiceAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new InvoicePaymentDetails(
                StripeInvoiceId: "in_test_123",
                StripePaymentIntentId: "pi_test_123",
                InvoicePeriodStart: DateTime.UtcNow.AddDays(-5),
                InvoicePeriodEnd: DateTime.UtcNow.AddDays(25),
                Currency: "EUR",
                AmountSubtotal: 19m,
                AmountTax: 4.18m,
                AmountTotal: 23.18m,
                AmountPaid: 23.18m,
                IsProrationApplied: false,
                ProrationAmount: 0m,
                BillingReason: "subscription_cycle"));
    }

    private CreateInvoiceRequestCommand CreateCommand(CustomerType customerType = CustomerType.Individual) =>
        new(
            CustomerType: customerType,
            FullName: "Mario Rossi",
            CompanyName: customerType == CustomerType.Company ? "ACME Srl" : null,
            Address: "Via Roma 1",
            City: "Milano",
            PostalCode: "20100",
            Country: "IT",
            FiscalCode: "RSSMRA80A01H501Z",
            VatNumber: customerType == CustomerType.Company ? "IT12345678901" : null,
            SdiCode: null,
            PecEmail: null,
            UserSubscriptionId: _subscriptionId,
            StripePaymentIntentId: "pi_test_123")
        {
            UserId = TestUserId,
            IpAddress = "127.0.0.1",
            UserAgent = "TestAgent"
        };

    [Fact]
    public async Task Should_Create_InvoiceRequest_And_Return_Id()
    {
        var result = await _handler.Handle(CreateCommand(), CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.Data.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task Should_Save_All_Fields_Correctly()
    {
        var result = await _handler.Handle(CreateCommand(), CancellationToken.None);

        var saved = await _dbContext.InvoiceRequests.FindAsync(result.Data);
        saved.Should().NotBeNull();
        saved!.UserId.Should().Be(TestUserId);
        saved.FullName.Should().Be("Mario Rossi");
        saved.Address.Should().Be("Via Roma 1");
        saved.City.Should().Be("Milano");
        saved.PostalCode.Should().Be("20100");
        saved.Country.Should().Be("IT");
        saved.CustomerType.Should().Be(CustomerType.Individual);
        saved.StripePaymentIntentId.Should().Be("pi_test_123");
        saved.StripeInvoiceId.Should().Be("in_test_123");
        saved.UserSubscriptionId.Should().Be(_subscriptionId);
        saved.ServiceName.Should().Be("Pro");
        saved.ServicePeriodStart.Should().NotBeNull();
        saved.ServicePeriodEnd.Should().NotBeNull();
        saved.AmountPaid.Should().Be(23.18m);
        saved.Currency.Should().Be("EUR");
        saved.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Should_Fail_When_Subscription_Does_Not_Belong_To_User()
    {
        var command = CreateCommand() with { UserSubscriptionId = Guid.NewGuid() };
        var result = await _handler.Handle(command, CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("Subscription reference not found for this user.");
    }

    [Fact]
    public async Task Should_Fail_When_Request_Already_Exists_For_Same_Billing_Transaction()
    {
        var command = CreateCommand();

        var firstResult = await _handler.Handle(command, CancellationToken.None);
        var secondResult = await _handler.Handle(command, CancellationToken.None);

        firstResult.Succeeded.Should().BeTrue();
        secondResult.Succeeded.Should().BeFalse();
        secondResult.Errors.Should().Contain("An invoice request already exists for this billing transaction.");
    }

    [Fact]
    public async Task Should_Call_AuditService_With_InvoiceRequestCreated()
    {
        var result = await _handler.Handle(CreateCommand(), CancellationToken.None);

        await _auditService.Received(1).LogAsync(
            AuditActions.InvoiceRequestCreated,
            entityType: "InvoiceRequest",
            entityId: result.Data.ToString(),
            details: Arg.Any<string>(),
            userId: TestUserId,
            ipAddress: "127.0.0.1",
            userAgent: "TestAgent",
            cancellationToken: Arg.Any<CancellationToken>());
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
