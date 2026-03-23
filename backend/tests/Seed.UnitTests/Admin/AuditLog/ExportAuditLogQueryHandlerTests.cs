using System.Text;
using FluentAssertions;
using NSubstitute;
using Seed.Application.Admin.AuditLog.Queries.ExportAuditLog;
using Seed.Application.Common.Interfaces;
using Seed.Domain.Entities;

namespace Seed.UnitTests.Admin.AuditLog;

public class ExportAuditLogQueryHandlerTests
{
    private readonly IAuditLogReader _auditLogReader;
    private readonly ExportAuditLogQueryHandler _handler;

    public ExportAuditLogQueryHandlerTests()
    {
        _auditLogReader = Substitute.For<IAuditLogReader>();
        _handler = new ExportAuditLogQueryHandler(_auditLogReader);
    }

    [Fact]
    public async Task Should_Generate_CSV_With_Correct_Header()
    {
        _auditLogReader.GetQueryable().Returns(new List<AuditLogEntry>().AsQueryable());

        var result = await _handler.Handle(new ExportAuditLogQuery(), CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        var csv = Encoding.UTF8.GetString(result.Data!);
        // Skip BOM
        csv = csv.TrimStart('\uFEFF');
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        lines[0].Trim().Should().Be("Id,Timestamp,UserId,Action,EntityType,EntityId,Details,IpAddress,UserAgent");
    }

    [Fact]
    public async Task Should_Export_Filtered_Entries()
    {
        var entries = new List<AuditLogEntry>
        {
            new() { Id = Guid.NewGuid(), Timestamp = DateTime.UtcNow, Action = "UserCreated", EntityType = "User" },
            new() { Id = Guid.NewGuid(), Timestamp = DateTime.UtcNow, Action = "LoginSuccess", EntityType = "Auth" }
        };
        _auditLogReader.GetQueryable().Returns(entries.AsQueryable());

        var query = new ExportAuditLogQuery { ActionFilter = "UserCreated" };
        var result = await _handler.Handle(query, CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        var csv = Encoding.UTF8.GetString(result.Data!).TrimStart('\uFEFF');
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        lines.Should().HaveCount(2); // header + 1 data row
        lines[1].Should().Contain("UserCreated");
    }

    [Fact]
    public async Task Should_Limit_To_10000_Rows()
    {
        var entries = Enumerable.Range(0, 10_500).Select(i => new AuditLogEntry
        {
            Id = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow.AddMinutes(-i),
            Action = "TestAction",
            EntityType = "Test"
        }).ToList();
        _auditLogReader.GetQueryable().Returns(entries.AsQueryable());

        var result = await _handler.Handle(new ExportAuditLogQuery(), CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        var csv = Encoding.UTF8.GetString(result.Data!).TrimStart('\uFEFF');
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        lines.Should().HaveCount(10_001); // header + 10000 data rows
    }
}
