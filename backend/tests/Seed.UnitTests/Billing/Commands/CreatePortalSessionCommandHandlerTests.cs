using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Seed.Application.Billing.Commands.CreatePortalSession;
using Seed.Application.Common.Interfaces;
using Seed.Domain.Entities;
using Seed.Domain.Enums;
using Seed.Infrastructure.Billing.Commands;
using Seed.Infrastructure.Persistence;

namespace Seed.UnitTests.Billing.Commands;

public class CreatePortalSessionCommandHandlerTests : IDisposable
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IPaymentGateway _paymentGateway;
    private readonly CreatePortalSessionCommandHandler _handler;

    private static readonly Guid TestUserId = Guid.NewGuid();

    public CreatePortalSessionCommandHandlerTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new ApplicationDbContext(options);
        _paymentGateway = Substitute.For<IPaymentGateway>();

        _paymentGateway.CreateCustomerPortalSessionAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("https://billing.stripe.com/session/test_portal");

        _handler = new CreatePortalSessionCommandHandler(_dbContext, _paymentGateway);
    }

    private CreatePortalSessionCommand CreateCommand() => new("https://example.com/account")
    {
        UserId = TestUserId
    };

    [Fact]
    public async Task Should_Return_PortalUrl_When_StripeCustomerId_Exists()
    {
        _dbContext.UserSubscriptions.Add(new UserSubscription
        {
            Id = Guid.NewGuid(),
            UserId = TestUserId,
            PlanId = Guid.NewGuid(),
            StripeCustomerId = "cus_existing_123",
            CurrentPeriodStart = DateTime.UtcNow,
            CurrentPeriodEnd = DateTime.UtcNow.AddMonths(1)
        });
        await _dbContext.SaveChangesAsync();

        var result = await _handler.Handle(CreateCommand(), CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.Data!.PortalUrl.Should().Be("https://billing.stripe.com/session/test_portal");
    }

    [Fact]
    public async Task Should_Return_Failure_When_No_StripeCustomerId_Found()
    {
        var result = await _handler.Handle(CreateCommand(), CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("No billing account found. Please subscribe to a plan first.");
    }

    [Fact]
    public async Task Should_Call_PaymentGateway_With_Correct_Parameters()
    {
        _dbContext.UserSubscriptions.Add(new UserSubscription
        {
            Id = Guid.NewGuid(),
            UserId = TestUserId,
            PlanId = Guid.NewGuid(),
            StripeCustomerId = "cus_correct_456",
            CurrentPeriodStart = DateTime.UtcNow,
            CurrentPeriodEnd = DateTime.UtcNow.AddMonths(1)
        });
        await _dbContext.SaveChangesAsync();

        await _handler.Handle(CreateCommand(), CancellationToken.None);

        await _paymentGateway.Received(1).CreateCustomerPortalSessionAsync(
            "cus_correct_456", "https://example.com/account", Arg.Any<CancellationToken>());
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
