using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Seed.Application.Admin.InvoiceRequests.Queries.GetInvoiceRequests;
using Seed.Domain.Entities;
using Seed.Domain.Enums;
using Seed.Infrastructure.Billing.Queries;
using Seed.Infrastructure.Persistence;

namespace Seed.UnitTests.Billing.Queries;

public class GetAdminInvoiceRequestsQueryHandlerTests : IDisposable
{
    private readonly ApplicationDbContext _dbContext;
    private readonly GetAdminInvoiceRequestsQueryHandler _handler;

    public GetAdminInvoiceRequestsQueryHandlerTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new ApplicationDbContext(options);
        _handler = new GetAdminInvoiceRequestsQueryHandler(_dbContext);
    }

    private static ApplicationUser CreateUser(string email) => new()
    {
        Id = Guid.NewGuid(),
        UserName = email,
        Email = email,
        FirstName = "Mario",
        LastName = "Rossi"
    };

    private static InvoiceRequest CreateInvoiceRequest(
        Guid userId,
        InvoiceRequestStatus status = InvoiceRequestStatus.Requested) => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        CustomerType = CustomerType.Individual,
        FullName = "Mario Rossi",
        Address = "Via Roma 1",
        City = "Milano",
        PostalCode = "20100",
        Country = "IT",
        Status = status,
        UserSubscriptionId = Guid.NewGuid(),
        ServiceName = "Pro",
        ServicePeriodStart = DateTime.UtcNow.AddDays(-10),
        ServicePeriodEnd = DateTime.UtcNow.AddDays(20),
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    [Fact]
    public async Task Should_Return_Empty_List_When_No_Requests()
    {
        var result = await _handler.Handle(
            new GetInvoiceRequestsQuery { PageNumber = 1, PageSize = 10 },
            CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.Data!.Items.Should().BeEmpty();
        result.Data.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task Should_Return_Paginated_Results()
    {
        var user1 = CreateUser("u1@test.com");
        var user2 = CreateUser("u2@test.com");
        var user3 = CreateUser("u3@test.com");
        _dbContext.Users.AddRange(user1, user2, user3);
        _dbContext.InvoiceRequests.AddRange(
            CreateInvoiceRequest(user1.Id),
            CreateInvoiceRequest(user2.Id),
            CreateInvoiceRequest(user3.Id));
        await _dbContext.SaveChangesAsync();

        var result = await _handler.Handle(
            new GetInvoiceRequestsQuery { PageNumber = 1, PageSize = 2 },
            CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.Data!.Items.Should().HaveCount(2);
        result.Data.TotalCount.Should().Be(3);
        result.Data.TotalPages.Should().Be(2);
        result.Data.HasNextPage.Should().BeTrue();
    }

    [Fact]
    public async Task Should_Filter_By_Status()
    {
        var user1 = CreateUser("f1@test.com");
        var user2 = CreateUser("f2@test.com");
        _dbContext.Users.AddRange(user1, user2);
        _dbContext.InvoiceRequests.AddRange(
            CreateInvoiceRequest(user1.Id, InvoiceRequestStatus.Requested),
            CreateInvoiceRequest(user2.Id, InvoiceRequestStatus.Issued));
        await _dbContext.SaveChangesAsync();

        var result = await _handler.Handle(
            new GetInvoiceRequestsQuery { PageNumber = 1, PageSize = 10, StatusFilter = "Requested" },
            CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.Data!.Items.Should().HaveCount(1);
        result.Data.Items[0].Status.Should().Be("Requested");
    }

    [Fact]
    public async Task Should_Include_User_Email()
    {
        var user = CreateUser("info@company.com");
        _dbContext.Users.Add(user);
        _dbContext.InvoiceRequests.Add(CreateInvoiceRequest(user.Id));
        await _dbContext.SaveChangesAsync();

        var result = await _handler.Handle(
            new GetInvoiceRequestsQuery { PageNumber = 1, PageSize = 10 },
            CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.Data!.Items.Should().HaveCount(1);
        result.Data.Items[0].UserEmail.Should().Be("info@company.com");
        result.Data.Items[0].UserFullName.Should().Be("Mario Rossi");
        result.Data.Items[0].ServiceName.Should().Be("Pro");
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
