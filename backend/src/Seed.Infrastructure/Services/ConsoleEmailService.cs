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
}
