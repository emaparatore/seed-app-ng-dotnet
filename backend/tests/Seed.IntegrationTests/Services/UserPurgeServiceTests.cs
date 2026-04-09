using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Seed.Application.Common.Interfaces;
using Seed.Domain.Authorization;
using Seed.Domain.Entities;
using Seed.Infrastructure.Persistence;
using Seed.IntegrationTests.Infrastructure;

namespace Seed.IntegrationTests.Services;

public class UserPurgeServiceTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    private async Task<(ApplicationUser User, IServiceScope Scope)> CreateTestUserAsync()
    {
        var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var user = new ApplicationUser
        {
            Email = $"purge-{Guid.NewGuid():N}@test.com",
            UserName = $"purge-{Guid.NewGuid():N}@test.com",
            FirstName = "Test",
            LastName = "User",
            IsActive = true,
            EmailConfirmed = true
        };

        var result = await userManager.CreateAsync(user, "Password1!");
        result.Succeeded.Should().BeTrue();

        return (user, scope);
    }

    [Fact]
    public async Task PurgeUserAsync_Anonymizes_Audit_Log_Entries()
    {
        var (user, scope) = await CreateTestUserAsync();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var purgeService = scope.ServiceProvider.GetRequiredService<IUserPurgeService>();

        // Create audit log entries for the user
        dbContext.AuditLogEntries.Add(new AuditLogEntry
        {
            Action = AuditActions.LoginSuccess,
            EntityType = "User",
            EntityId = user.Id.ToString(),
            UserId = user.Id,
            Details = $"Email: {user.Email}",
            IpAddress = "192.168.1.1",
            UserAgent = "Mozilla/5.0"
        });
        dbContext.AuditLogEntries.Add(new AuditLogEntry
        {
            Action = AuditActions.PasswordChanged,
            EntityType = "User",
            UserId = user.Id,
            Details = "Password changed",
            IpAddress = "10.0.0.1",
            UserAgent = "Chrome"
        });
        await dbContext.SaveChangesAsync();

        await purgeService.PurgeUserAsync(user.Id);

        // Verify audit log entries are anonymized
        var entries = await dbContext.AuditLogEntries
            .Where(a => a.Action == AuditActions.LoginSuccess || a.Action == AuditActions.PasswordChanged)
            .Where(a => a.Details == "[REDACTED]")
            .ToListAsync();

        entries.Should().HaveCountGreaterThanOrEqualTo(2);
        entries.Should().AllSatisfy(e =>
        {
            e.UserId.Should().BeNull();
            e.Details.Should().Be("[REDACTED]");
            e.IpAddress.Should().BeNull();
            e.UserAgent.Should().BeNull();
        });

        // EntityId should be null for entries that had the user's id
        var entityEntries = entries.Where(e => e.Action == AuditActions.LoginSuccess);
        entityEntries.Should().AllSatisfy(e => e.EntityId.Should().BeNull());

        scope.Dispose();
    }

    [Fact]
    public async Task PurgeUserAsync_Deletes_Refresh_Tokens()
    {
        var (user, scope) = await CreateTestUserAsync();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var purgeService = scope.ServiceProvider.GetRequiredService<IUserPurgeService>();

        // Create refresh tokens
        dbContext.RefreshTokens.Add(new RefreshToken
        {
            Token = Guid.NewGuid().ToString(),
            UserId = user.Id,
            ExpiresAt = DateTime.UtcNow.AddDays(7)
        });
        dbContext.RefreshTokens.Add(new RefreshToken
        {
            Token = Guid.NewGuid().ToString(),
            UserId = user.Id,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            RevokedAt = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync();

        await purgeService.PurgeUserAsync(user.Id);

        var tokens = await dbContext.RefreshTokens
            .Where(r => r.UserId == user.Id)
            .ToListAsync();
        tokens.Should().BeEmpty();

        scope.Dispose();
    }

    [Fact]
    public async Task PurgeUserAsync_Deletes_User_From_Database()
    {
        var (user, scope) = await CreateTestUserAsync();
        var purgeService = scope.ServiceProvider.GetRequiredService<IUserPurgeService>();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        await purgeService.PurgeUserAsync(user.Id);

        var deletedUser = await dbContext.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == user.Id);
        deletedUser.Should().BeNull();

        scope.Dispose();
    }

    [Fact]
    public async Task PurgeUserAsync_With_NonExistent_UserId_Does_Not_Throw()
    {
        var scope = factory.Services.CreateScope();
        var purgeService = scope.ServiceProvider.GetRequiredService<IUserPurgeService>();

        var act = () => purgeService.PurgeUserAsync(Guid.NewGuid());

        await act.Should().NotThrowAsync();

        scope.Dispose();
    }
}
