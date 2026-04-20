using Microsoft.Extensions.Logging;
using Seed.Application.Common.Interfaces;

namespace Seed.Infrastructure.Services;

public sealed class ConsoleEmailService(ILogger<ConsoleEmailService> logger) : IEmailService
{
    public Task SendPasswordResetEmailAsync(string toEmail, string resetLink, CancellationToken cancellationToken = default)
    {
        logger.LogWarning("SMTP not configured — logging email to console");
        logger.LogInformation(
            "Password Reset Email → To: {Email}, Link: {Link}",
            toEmail,
            resetLink);

        return Task.CompletedTask;
    }

    public Task SendEmailVerificationAsync(string toEmail, string verificationLink, CancellationToken cancellationToken = default)
    {
        logger.LogWarning("SMTP not configured — logging email to console");
        logger.LogInformation(
            "Email Verification → To: {Email}, Link: {Link}",
            toEmail,
            verificationLink);

        return Task.CompletedTask;
    }

    public Task SendSubscriptionConfirmationAsync(string toEmail, string planName, CancellationToken ct = default)
    {
        logger.LogWarning("SMTP not configured — logging email to console");
        logger.LogInformation(
            "Subscription Confirmation → To: {Email}, Plan: {PlanName}",
            toEmail,
            planName);

        return Task.CompletedTask;
    }

    public Task SendTrialEndingNotificationAsync(string toEmail, string planName, int daysRemaining, CancellationToken ct = default)
    {
        logger.LogWarning("SMTP not configured — logging email to console");
        logger.LogInformation(
            "Trial Ending Notification → To: {Email}, Plan: {PlanName}, DaysRemaining: {DaysRemaining}",
            toEmail,
            planName,
            daysRemaining);

        return Task.CompletedTask;
    }

    public Task SendSubscriptionCanceledAsync(string toEmail, string planName, DateTime endDate, CancellationToken ct = default)
    {
        logger.LogWarning("SMTP not configured — logging email to console");
        logger.LogInformation(
            "Subscription Canceled → To: {Email}, Plan: {PlanName}, EndDate: {EndDate:u}",
            toEmail,
            planName,
            endDate);

        return Task.CompletedTask;
    }
}
