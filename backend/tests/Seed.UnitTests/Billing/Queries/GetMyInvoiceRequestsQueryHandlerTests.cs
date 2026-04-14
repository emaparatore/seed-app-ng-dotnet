using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Seed.Application.Billing.Queries.GetMyInvoiceRequests;
using Seed.Domain.Entities;
using Seed.Domain.Enums;
using Seed.Infrastructure.Billing.Queries;
using Seed.Infrastructure.Persistence;

namespace Seed.UnitTests.Billing.Queries;

public class GetMyInvoiceRequestsQueryHandlerTests : IDisposable
{
    private readonly ApplicationDbContext _dbContext;
    private readonly GetMyInvoiceRequestsQueryHandler _handler;

    private static readonly Guid TestUserId = Guid.NewGuid();
    private static readonly Guid OtherUserId = Guid.NewGuid();

    public GetMyInvoiceRequestsQueryHandlerTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new ApplicationDbContext(options);
        _handler = new GetMyInvoiceRequestsQueryHandler(_dbContext);
    }

    private static InvoiceRequest CreateInvoiceRequest(Guid userId, DateTime? createdAt = null) => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        CustomerType = CustomerType.Individual,
        FullName = "Mario Rossi",
        Address = "Via Roma 1",
        City = "Milano",
        PostalCode = "20100",
        Country = "IT",
        Status = InvoiceRequestStatus.Requested,
        CreatedAt = createdAt ?? DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    [Fact]
    public async Task Should_Return_Empty_List_When_No_Requests()
    {
        var result = await _handler.Handle(new GetMyInvoiceRequestsQuery(TestUserId), CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.Data.Should().BeEmpty();
    }

    [Fact]
    public async Task Should_Return_Only_User_Requests()
    {
        _dbContext.InvoiceRequests.AddRange(
            CreateInvoiceRequest(TestUserId),
            CreateInvoiceRequest(TestUserId),
            CreateInvoiceRequest(OtherUserId));
        await _dbContext.SaveChangesAsync();

        var result = await _handler.Handle(new GetMyInvoiceRequestsQuery(TestUserId), CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.Data.Should().HaveCount(2);
        result.Data!.All(r => r.CustomerType == CustomerType.Individual.ToString()).Should().BeTrue();
    }

    [Fact]
    public async Task Should_Return_Requests_Ordered_By_CreatedAt_Descending()
    {
        var older = CreateInvoiceRequest(TestUserId, DateTime.UtcNow.AddDays(-2));
        var newer = CreateInvoiceRequest(TestUserId, DateTime.UtcNow.AddDays(-1));
        var newest = CreateInvoiceRequest(TestUserId, DateTime.UtcNow);
        _dbContext.InvoiceRequests.AddRange(older, newest, newer);
        await _dbContext.SaveChangesAsync();

        var result = await _handler.Handle(new GetMyInvoiceRequestsQuery(TestUserId), CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.Data!.Should().HaveCount(3);
        result.Data[0].CreatedAt.Should().BeAfter(result.Data[1].CreatedAt);
        result.Data[1].CreatedAt.Should().BeAfter(result.Data[2].CreatedAt);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
