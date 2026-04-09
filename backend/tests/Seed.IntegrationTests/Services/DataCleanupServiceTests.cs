using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Seed.Application.Common.Interfaces;
using Seed.Domain.Entities;
using Seed.Infrastructure.Persistence;
using Seed.IntegrationTests.Infrastructure;

namespace Seed.IntegrationTests.Services;

public class DataCleanupServiceTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    private async Task<ApplicationUser> CreateSoftDeletedUserAsync(
        IServiceScope scope, int deletedDaysAgo)
    {
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var user = new ApplicationUser
        {
            Email = $"cleanup-{Guid.NewGuid():N}@test.com",
            UserName = $"cleanup-{Guid.NewGuid():N}@test.com",
            FirstName = "Test",
            LastName = "User",
            IsActive = true,
            EmailConfirmed = true,
            IsDeleted = true,
            DeletedAt = DateTime.UtcNow.AddDays(-deletedDaysAgo)
        };

        var result = await userManager.CreateAsync(user, "Password1!");
        result.Succeeded.Should().BeTrue();

        return user;
    }

    [Fact]
    public async Task PurgeSoftDeletedUsersAsync_Purges_Users_Past_Retention_Period()
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var cleanupService = scope.ServiceProvider.GetRequiredService<IDataCleanupService>();

        var user = await CreateSoftDeletedUserAsync(scope, deletedDaysAgo: 31);

        var purged = await cleanupService.PurgeSoftDeletedUsersAsync();

        purged.Should().BeGreaterThanOrEqualTo(1);

        var found = await dbContext.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == user.Id);
        found.Should().BeNull();
    }

    [Fact]
    public async Task PurgeSoftDeletedUsersAsync_Does_Not_Purge_Recently_Deleted_Users()
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var cleanupService = scope.ServiceProvider.GetRequiredService<IDataCleanupService>();

        var user = await CreateSoftDeletedUserAsync(scope, deletedDaysAgo: 5);

        await cleanupService.PurgeSoftDeletedUsersAsync();

        var found = await dbContext.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == user.Id);
        found.Should().NotBeNull();
    }

    [Fact]
    public async Task CleanupExpiredRefreshTokensAsync_Deletes_Expired_Tokens()
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var cleanupService = scope.ServiceProvider.GetRequiredService<IDataCleanupService>();

        // Create a user to own the token
        var user = new ApplicationUser
        {
            Email = $"token-{Guid.NewGuid():N}@test.com",
            UserName = $"token-{Guid.NewGuid():N}@test.com",
            FirstName = "Test",
            LastName = "User",
            IsActive = true,
            EmailConfirmed = true
        };
        (await userManager.CreateAsync(user, "Password1!")).Succeeded.Should().BeTrue();

        // Token expired 2 hours ago — should be deleted immediately regardless of retention period
        var token = new RefreshToken
        {
            Token = Guid.NewGuid().ToString(),
            UserId = user.Id,
            ExpiresAt = DateTime.UtcNow.AddHours(-2),
            CreatedAt = DateTime.UtcNow.AddDays(-1)
        };
        dbContext.RefreshTokens.Add(token);
        await dbContext.SaveChangesAsync();

        var deleted = await cleanupService.CleanupExpiredRefreshTokensAsync();

        deleted.Should().BeGreaterThanOrEqualTo(1);

        var found = await dbContext.RefreshTokens.FirstOrDefaultAsync(r => r.Id == token.Id);
        found.Should().BeNull();
    }

    [Fact]
    public async Task CleanupExpiredRefreshTokensAsync_Keeps_Active_Tokens()
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var cleanupService = scope.ServiceProvider.GetRequiredService<IDataCleanupService>();

        var user = new ApplicationUser
        {
            Email = $"token-{Guid.NewGuid():N}@test.com",
            UserName = $"token-{Guid.NewGuid():N}@test.com",
            FirstName = "Test",
            LastName = "User",
            IsActive = true,
            EmailConfirmed = true
        };
        (await userManager.CreateAsync(user, "Password1!")).Succeeded.Should().BeTrue();

        // Token still valid — should not be deleted
        var token = new RefreshToken
        {
            Token = Guid.NewGuid().ToString(),
            UserId = user.Id,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow
        };
        dbContext.RefreshTokens.Add(token);
        await dbContext.SaveChangesAsync();

        await cleanupService.CleanupExpiredRefreshTokensAsync();

        var found = await dbContext.RefreshTokens.FirstOrDefaultAsync(r => r.Id == token.Id);
        found.Should().NotBeNull();
    }

    [Fact]
    public async Task CleanupOldAuditLogEntriesAsync_Deletes_Old_Entries()
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var cleanupService = scope.ServiceProvider.GetRequiredService<IDataCleanupService>();

        var entry = new AuditLogEntry
        {
            Action = "TestCleanup",
            EntityType = "Test",
            Timestamp = DateTime.UtcNow.AddDays(-366)
        };
        dbContext.AuditLogEntries.Add(entry);
        await dbContext.SaveChangesAsync();

        var deleted = await cleanupService.CleanupOldAuditLogEntriesAsync();

        deleted.Should().BeGreaterThanOrEqualTo(1);

        var found = await dbContext.AuditLogEntries.FirstOrDefaultAsync(a => a.Id == entry.Id);
        found.Should().BeNull();
    }

    [Fact]
    public async Task CleanupOldAuditLogEntriesAsync_Keeps_Recent_Entries()
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var cleanupService = scope.ServiceProvider.GetRequiredService<IDataCleanupService>();

        var entry = new AuditLogEntry
        {
            Action = "TestKeep",
            EntityType = "Test",
            Timestamp = DateTime.UtcNow.AddDays(-30)
        };
        dbContext.AuditLogEntries.Add(entry);
        await dbContext.SaveChangesAsync();

        await cleanupService.CleanupOldAuditLogEntriesAsync();

        var found = await dbContext.AuditLogEntries.FirstOrDefaultAsync(a => a.Id == entry.Id);
        found.Should().NotBeNull();
    }
}
