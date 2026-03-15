using Microsoft.Extensions.Logging;
using Seed.Application.Common.Interfaces;

namespace Seed.Infrastructure.Services;

public sealed class ConsoleEmailService(ILogger<ConsoleEmailService> logger) : IEmailService
{
    public Task SendPasswordResetEmailAsync(string toEmail, string resetToken, CancellationToken cancellationToken = default)
    {
        logger.LogWarning("SMTP not configured — logging email to console");
        logger.LogInformation(
            "Password Reset Email → To: {Email}, Token: {Token}",
            toEmail,
            resetToken);

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
