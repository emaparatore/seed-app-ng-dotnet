using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Seed.Application.Common.Interfaces;
using Seed.Domain.Entities;
using Seed.Domain.Enums;
using Seed.Infrastructure.Persistence;

namespace Seed.Infrastructure.Services;

public sealed class UserPurgeService(
    ApplicationDbContext dbContext,
    UserManager<ApplicationUser> userManager,
    IServiceProvider serviceProvider,
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

        // 2a. Cancel active Stripe subscriptions, delete Stripe customer, and anonymize subscription records
        var paymentGateway = serviceProvider.GetService<IPaymentGateway>();
        if (paymentGateway is not null)
        {
            var subscriptions = await dbContext.UserSubscriptions
                .Where(s => s.UserId == userId)
                .ToListAsync(cancellationToken);

            foreach (var sub in subscriptions)
            {
                if (!string.IsNullOrWhiteSpace(sub.StripeSubscriptionId)
                    && (sub.Status == SubscriptionStatus.Active || sub.Status == SubscriptionStatus.Trialing))
                {
                    try
                    {
                        await paymentGateway.CancelSubscriptionAsync(sub.StripeSubscriptionId, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Failed to cancel Stripe subscription {SubscriptionId}", sub.StripeSubscriptionId);
                    }
                }

                sub.UserId = null;
                sub.Status = SubscriptionStatus.Canceled;
                sub.UpdatedAt = DateTime.UtcNow;
            }

            var stripeCustomerId = subscriptions
                .FirstOrDefault(s => !string.IsNullOrWhiteSpace(s.StripeCustomerId))
                ?.StripeCustomerId;

            if (!string.IsNullOrWhiteSpace(stripeCustomerId))
            {
                try
                {
                    await paymentGateway.DeleteCustomerAsync(stripeCustomerId, cancellationToken);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to delete Stripe customer {CustomerId}", stripeCustomerId);
                }
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            // 2b. Anonymize invoice requests (retain for fiscal compliance, remove personal data)
            var invoiceRequests = await dbContext.InvoiceRequests
                .Where(i => i.UserId == userId)
                .ToListAsync(cancellationToken);

            foreach (var ir in invoiceRequests)
            {
                ir.UserId = null;
                ir.FullName = "ANONYMIZED";
                ir.CompanyName = ir.CompanyName is not null ? "ANONYMIZED" : null;
                ir.Address = "ANONYMIZED";
                ir.City = "ANONYMIZED";
                ir.PostalCode = "ANONYMIZED";
                ir.PecEmail = null;
                // Keep: Country (aggregate stats), FiscalCode, VatNumber, SdiCode (fiscal compliance),
                //        StripePaymentIntentId, CustomerType, Status, ProcessedAt, CreatedAt, UpdatedAt
                ir.UpdatedAt = DateTime.UtcNow;
            }

            await dbContext.SaveChangesAsync(cancellationToken);
        }

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
