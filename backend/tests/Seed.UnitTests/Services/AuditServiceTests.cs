using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Seed.Domain.Authorization;
using Seed.Infrastructure.Persistence;
using Seed.Infrastructure.Services;

namespace Seed.UnitTests.Services;

public class AuditServiceTests : IDisposable
{
    private readonly ApplicationDbContext _dbContext;
    private readonly AuditService _auditService;

    public AuditServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _dbContext = new ApplicationDbContext(options);
        var logger = Substitute.For<ILogger<AuditService>>();
        _auditService = new AuditService(_dbContext, logger);
    }

    [Fact]
    public async Task LogAsync_Creates_AuditLogEntry_In_Database()
    {
        await _auditService.LogAsync(AuditActions.LoginSuccess, "User");

        var entries = await _dbContext.AuditLogEntries.ToListAsync();
        entries.Should().HaveCount(1);
        entries[0].Action.Should().Be(AuditActions.LoginSuccess);
        entries[0].EntityType.Should().Be("User");
    }

    [Fact]
    public async Task LogAsync_Sets_All_Fields_Correctly()
    {
        var userId = Guid.NewGuid();
        await _auditService.LogAsync(
            AuditActions.PasswordChanged, "User", userId.ToString(),
            "Password changed", userId, "192.168.1.1", "Mozilla/5.0");

        var entry = await _dbContext.AuditLogEntries.SingleAsync();
        entry.Action.Should().Be(AuditActions.PasswordChanged);
        entry.EntityType.Should().Be("User");
        entry.EntityId.Should().Be(userId.ToString());
        entry.Details.Should().Be("Password changed");
        entry.UserId.Should().Be(userId);
        entry.IpAddress.Should().Be("192.168.1.1");
        entry.UserAgent.Should().Be("Mozilla/5.0");
        entry.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        entry.Id.Should().NotBeEmpty();
    }

    [Fact]
    public async Task LogAsync_Works_With_Null_UserId()
    {
        await _auditService.LogAsync(AuditActions.SystemSeeding, "System", details: "Initial seed");

        var entry = await _dbContext.AuditLogEntries.SingleAsync();
        entry.UserId.Should().BeNull();
        entry.Action.Should().Be(AuditActions.SystemSeeding);
    }

    [Fact]
    public async Task LogAsync_Does_Not_Propagate_Exceptions()
    {
        // Dispose context to force an exception on SaveChanges
        _dbContext.Dispose();

        var act = () => _auditService.LogAsync(AuditActions.LoginSuccess, "User");

        await act.Should().NotThrowAsync();
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
