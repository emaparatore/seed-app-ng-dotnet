using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Seed.Application.Common.Interfaces;
using Seed.Domain.Entities;
using Seed.Infrastructure.Persistence;

namespace Seed.Infrastructure.Services;

public sealed class UserPurgeService(
    ApplicationDbContext dbContext,
    UserManager<ApplicationUser> userManager,
    ILogger<UserPurgeService> logger) : IUserPurgeService
{
    public async Task PurgeUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        // 1. Anonymize audit log entries for this user
        var auditEntries = await dbContext.AuditLogEntries
            .Where(a => a.UserId == userId)
            .ToListAsync(cancellationToken);

        foreach (var entry in auditEntries)
        {
            entry.UserId = null;
            entry.Details = "[REDACTED]";
            entry.IpAddress = null;
            entry.UserAgent = null;
        }

        // Also anonymize entries where EntityId matches the userId (e.g. actions performed on this user)
        var entityEntries = await dbContext.AuditLogEntries
            .Where(a => a.EntityId == userId.ToString())
            .ToListAsync(cancellationToken);

        foreach (var entry in entityEntries)
        {
            entry.EntityId = null;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        // 2. Hard delete all refresh tokens for this user
        await dbContext.RefreshTokens
            .Where(r => r.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken);

        // 3. Delete the user record via UserManager (bypassing query filter for soft-deleted users)
        var user = await dbContext.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user is null)
        {
            logger.LogWarning("User {UserId} not found during purge — skipping", userId);
            return;
        }

        var result = await userManager.DeleteAsync(user);
        if (!result.Succeeded)
        {
            logger.LogError("Failed to delete user {UserId}: {Errors}", userId,
                string.Join(", ", result.Errors.Select(e => e.Description)));
        }
    }
}
