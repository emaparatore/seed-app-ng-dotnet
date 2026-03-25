using FluentAssertions;
using NSubstitute;
using Seed.Application.Admin.AuditLog.Queries.GetAuditLogEntries;
using Seed.Application.Common.Interfaces;
using Seed.Domain.Entities;

namespace Seed.UnitTests.Admin.AuditLog;

public class GetAuditLogEntriesQueryHandlerTests
{
    private readonly IAuditLogReader _auditLogReader;
    private readonly GetAuditLogEntriesQueryHandler _handler;

    public GetAuditLogEntriesQueryHandlerTests()
    {
        _auditLogReader = Substitute.For<IAuditLogReader>();
        _handler = new GetAuditLogEntriesQueryHandler(_auditLogReader);
    }

    private static List<AuditLogEntry> CreateTestEntries()
    {
        return
        [
            new AuditLogEntry
            {
                Id = Guid.NewGuid(), Timestamp = DateTime.UtcNow.AddHours(-1),
                UserId = Guid.NewGuid(), Action = "UserCreated", EntityType = "User",
                EntityId = "1", Details = "{\"email\":\"test@example.com\"}", IpAddress = "127.0.0.1"
            },
            new AuditLogEntry
            {
                Id = Guid.NewGuid(), Timestamp = DateTime.UtcNow.AddHours(-2),
                UserId = Guid.NewGuid(), Action = "LoginSuccess", EntityType = "Auth",
                EntityId = null, Details = "{\"method\":\"password\"}", IpAddress = "192.168.1.1"
            },
            new AuditLogEntry
            {
                Id = Guid.NewGuid(), Timestamp = DateTime.UtcNow.AddHours(-3),
                UserId = null, Action = "SystemSeeding", EntityType = "System",
                EntityId = null, Details = "{\"seeder\":\"roles\"}", IpAddress = null
            },
            new AuditLogEntry
            {
                Id = Guid.NewGuid(), Timestamp = DateTime.UtcNow.AddHours(-4),
                UserId = Guid.NewGuid(), Action = "UserCreated", EntityType = "User",
                EntityId = "2", Details = "{\"email\":\"admin@example.com\"}", IpAddress = "10.0.0.1"
            },
            new AuditLogEntry
            {
                Id = Guid.NewGuid(), Timestamp = DateTime.UtcNow.AddHours(-5),
                UserId = Guid.NewGuid(), Action = "PasswordChanged", EntityType = "User",
                EntityId = "3", Details = null, IpAddress = "127.0.0.1"
            }
        ];
    }

    [Fact]
    public async Task Should_Filter_By_Action()
    {
        var entries = CreateTestEntries();
        _auditLogReader.GetQueryable().Returns(entries.AsQueryable());

        var query = new GetAuditLogEntriesQuery { ActionFilter = "UserCreated" };
        var result = await _handler.Handle(query, CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.Data!.TotalCount.Should().Be(2);
        result.Data.Items.Should().AllSatisfy(i => i.Action.Should().Be("UserCreated"));
    }

    [Fact]
    public async Task Should_Filter_By_UserId()
    {
        var entries = CreateTestEntries();
        var targetUserId = entries[0].UserId!.Value;
        _auditLogReader.GetQueryable().Returns(entries.AsQueryable());

        var query = new GetAuditLogEntriesQuery { UserId = targetUserId };
        var result = await _handler.Handle(query, CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.Data!.TotalCount.Should().Be(1);
        result.Data.Items[0].UserId.Should().Be(targetUserId);
    }

    [Fact]
    public async Task Should_Filter_By_DateRange()
    {
        var entries = CreateTestEntries();
        _auditLogReader.GetQueryable().Returns(entries.AsQueryable());

        var query = new GetAuditLogEntriesQuery
        {
            DateFrom = DateTime.UtcNow.AddHours(-2.5),
            DateTo = DateTime.UtcNow
        };
        var result = await _handler.Handle(query, CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.Data!.TotalCount.Should().Be(2); // -1h, -2h
    }

    [Fact]
    public async Task Should_Search_In_Details()
    {
        var entries = CreateTestEntries();
        _auditLogReader.GetQueryable().Returns(entries.AsQueryable());

        var query = new GetAuditLogEntriesQuery { SearchTerm = "admin@example" };
        var result = await _handler.Handle(query, CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.Data!.TotalCount.Should().Be(1);
        result.Data.Items[0].Details.Should().Contain("admin@example.com");
    }

    [Fact]
    public async Task Should_Sort_Descending_By_Default()
    {
        var entries = CreateTestEntries();
        _auditLogReader.GetQueryable().Returns(entries.AsQueryable());

        var query = new GetAuditLogEntriesQuery();
        var result = await _handler.Handle(query, CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.Data!.Items.Should().BeInDescendingOrder(i => i.Timestamp);
    }

    [Fact]
    public async Task Should_Paginate_Results()
    {
        var entries = CreateTestEntries();
        _auditLogReader.GetQueryable().Returns(entries.AsQueryable());

        var query = new GetAuditLogEntriesQuery { PageNumber = 2, PageSize = 2 };
        var result = await _handler.Handle(query, CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.Data!.TotalCount.Should().Be(5);
        result.Data.Items.Should().HaveCount(2);
        result.Data.PageNumber.Should().Be(2);
        result.Data.PageSize.Should().Be(2);
        result.Data.TotalPages.Should().Be(3);
    }
}
