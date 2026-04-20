using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Seed.Application.Admin.InvoiceRequests.Commands.UpdateInvoiceRequestStatus;
using Seed.Application.Common.Interfaces;
using Seed.Domain.Authorization;
using Seed.Domain.Entities;
using Seed.Domain.Enums;
using Seed.Infrastructure.Billing.Commands;
using Seed.Infrastructure.Persistence;

namespace Seed.UnitTests.Billing.Commands;

public class UpdateInvoiceRequestStatusCommandHandlerTests : IDisposable
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IAuditService _auditService;
    private readonly UpdateInvoiceRequestStatusCommandHandler _handler;

    private static readonly Guid TestAdminId = Guid.NewGuid();

    public UpdateInvoiceRequestStatusCommandHandlerTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new ApplicationDbContext(options);
        _auditService = Substitute.For<IAuditService>();
        _handler = new UpdateInvoiceRequestStatusCommandHandler(_dbContext, _auditService);
    }

    private static InvoiceRequest CreateInvoiceRequest() => new()
    {
        Id = Guid.NewGuid(),
        UserId = Guid.NewGuid(),
        CustomerType = CustomerType.Individual,
        FullName = "Mario Rossi",
        Address = "Via Roma 1",
        City = "Milano",
        PostalCode = "20100",
        Country = "IT",
        Status = InvoiceRequestStatus.Requested,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    private UpdateInvoiceRequestStatusCommand CreateCommand(Guid invoiceRequestId, InvoiceRequestStatus newStatus) =>
        new(newStatus)
        {
            InvoiceRequestId = invoiceRequestId,
            CurrentUserId = TestAdminId,
            IpAddress = "127.0.0.1",
            UserAgent = "TestAgent"
        };

    [Fact]
    public async Task Should_Update_Status_Successfully()
    {
        var request = CreateInvoiceRequest();
        _dbContext.InvoiceRequests.Add(request);
        await _dbContext.SaveChangesAsync();

        var result = await _handler.Handle(CreateCommand(request.Id, InvoiceRequestStatus.InProgress), CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        var updated = await _dbContext.InvoiceRequests.FindAsync(request.Id);
        updated!.Status.Should().Be(InvoiceRequestStatus.InProgress);
        updated.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Should_Return_Failure_When_Not_Found()
    {
        var result = await _handler.Handle(CreateCommand(Guid.NewGuid(), InvoiceRequestStatus.InProgress), CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("Invoice request not found.");
    }

    [Fact]
    public async Task Should_Set_ProcessedAt_When_Status_Is_Issued()
    {
        var request = CreateInvoiceRequest();
        _dbContext.InvoiceRequests.Add(request);
        await _dbContext.SaveChangesAsync();

        await _handler.Handle(CreateCommand(request.Id, InvoiceRequestStatus.Issued), CancellationToken.None);

        var updated = await _dbContext.InvoiceRequests.FindAsync(request.Id);
        updated!.ProcessedAt.Should().NotBeNull();
        updated.ProcessedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Should_Not_Set_ProcessedAt_When_Status_Is_Not_Issued()
    {
        var request = CreateInvoiceRequest();
        _dbContext.InvoiceRequests.Add(request);
        await _dbContext.SaveChangesAsync();

        await _handler.Handle(CreateCommand(request.Id, InvoiceRequestStatus.InProgress), CancellationToken.None);

        var updated = await _dbContext.InvoiceRequests.FindAsync(request.Id);
        updated!.ProcessedAt.Should().BeNull();
    }

    [Fact]
    public async Task Should_Call_AuditService_With_InvoiceRequestStatusUpdated()
    {
        var request = CreateInvoiceRequest();
        _dbContext.InvoiceRequests.Add(request);
        await _dbContext.SaveChangesAsync();

        await _handler.Handle(CreateCommand(request.Id, InvoiceRequestStatus.Issued), CancellationToken.None);

        await _auditService.Received(1).LogAsync(
            AuditActions.InvoiceRequestStatusUpdated,
            entityType: "InvoiceRequest",
            entityId: request.Id.ToString(),
            details: Arg.Any<string>(),
            userId: TestAdminId,
            ipAddress: "127.0.0.1",
            userAgent: "TestAgent",
            cancellationToken: Arg.Any<CancellationToken>());
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
