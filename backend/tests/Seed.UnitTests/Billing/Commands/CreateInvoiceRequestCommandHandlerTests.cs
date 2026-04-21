using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Seed.Application.Billing.Commands.CreateInvoiceRequest;
using Seed.Application.Common.Interfaces;
using Seed.Domain.Authorization;
using Seed.Domain.Enums;
using Seed.Infrastructure.Billing.Commands;
using Seed.Infrastructure.Persistence;

namespace Seed.UnitTests.Billing.Commands;

public class CreateInvoiceRequestCommandHandlerTests : IDisposable
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IAuditService _auditService;
    private readonly CreateInvoiceRequestCommandHandler _handler;

    private static readonly Guid TestUserId = Guid.NewGuid();

    public CreateInvoiceRequestCommandHandlerTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new ApplicationDbContext(options);
        _auditService = Substitute.For<IAuditService>();
        _handler = new CreateInvoiceRequestCommandHandler(_dbContext, _auditService);
    }

    private static CreateInvoiceRequestCommand CreateCommand(CustomerType customerType = CustomerType.Individual) =>
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
        saved.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
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
