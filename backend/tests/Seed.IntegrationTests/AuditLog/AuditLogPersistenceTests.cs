using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Seed.Application.Common.Interfaces;
using Seed.Domain.Authorization;
using Seed.Infrastructure.Persistence;
using Seed.IntegrationTests.Infrastructure;

namespace Seed.IntegrationTests.AuditLog;

public class AuditLogPersistenceTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    [Fact]
    public async Task AuditLogEntry_Is_Persisted_In_Database()
    {
        using var scope = factory.Services.CreateScope();
        var auditService = scope.ServiceProvider.GetRequiredService<IAuditService>();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var userId = Guid.NewGuid();
        await auditService.LogAsync(
            AuditActions.LoginSuccess, "User", userId.ToString(),
            "Test login", userId, "127.0.0.1", "TestAgent/1.0");

        var entry = await dbContext.AuditLogEntries
            .FirstOrDefaultAsync(e => e.UserId == userId && e.Action == AuditActions.LoginSuccess);

        entry.Should().NotBeNull();
        entry!.EntityType.Should().Be("User");
        entry.EntityId.Should().Be(userId.ToString());
        entry.Details.Should().Be("Test login");
        entry.IpAddress.Should().Be("127.0.0.1");
        entry.UserAgent.Should().Be("TestAgent/1.0");
    }

    [Fact]
    public async Task AuditLogEntries_Can_Be_Queried_By_Timestamp()
    {
        using var scope = factory.Services.CreateScope();
        var auditService = scope.ServiceProvider.GetRequiredService<IAuditService>();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var before = DateTime.UtcNow;
        await auditService.LogAsync(AuditActions.UserCreated, "User", details: "timestamp-query-test");
        var after = DateTime.UtcNow;

        var entries = await dbContext.AuditLogEntries
            .Where(e => e.Timestamp >= before && e.Timestamp <= after && e.Details == "timestamp-query-test")
            .ToListAsync();

        entries.Should().HaveCount(1);
    }

    [Fact]
    public async Task AuditLogEntries_Can_Be_Queried_By_Action()
    {
        using var scope = factory.Services.CreateScope();
        var auditService = scope.ServiceProvider.GetRequiredService<IAuditService>();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        await auditService.LogAsync(AuditActions.PasswordChanged, "User", details: "action-query-test");

        var entries = await dbContext.AuditLogEntries
            .Where(e => e.Action == AuditActions.PasswordChanged && e.Details == "action-query-test")
            .ToListAsync();

        entries.Should().NotBeEmpty();
    }

    [Fact]
    public async Task SuperAdmin_Seeding_Generates_Audit_Log_Entry()
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var seedingEntries = await dbContext.AuditLogEntries
            .Where(e => e.Action == AuditActions.SystemSeeding)
            .ToListAsync();

        seedingEntries.Should().NotBeEmpty();
        seedingEntries.Should().Contain(e => e.Details != null && e.Details.Contains("SuperAdmin"));
    }
}
