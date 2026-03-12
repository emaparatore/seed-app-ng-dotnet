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
}
