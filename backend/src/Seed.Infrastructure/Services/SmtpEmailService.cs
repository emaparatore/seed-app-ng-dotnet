using MailKit.Net.Smtp;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using Seed.Application.Common.Interfaces;
using Seed.Shared.Configuration;

namespace Seed.Infrastructure.Services;

public sealed class SmtpEmailService(
    IOptions<SmtpSettings> smtpSettings,
    ILogger<SmtpEmailService> logger) : IEmailService
{
    private readonly SmtpSettings _settings = smtpSettings.Value;

    public async Task SendPasswordResetEmailAsync(string toEmail, string resetToken, CancellationToken cancellationToken = default)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_settings.FromName, _settings.FromEmail));
        message.To.Add(MailboxAddress.Parse(toEmail));
        message.Subject = "Password Reset Request";
        message.Body = new TextPart("html")
        {
            Text = $"""
                <h2>Password Reset</h2>
                <p>You requested a password reset. Use the following token to reset your password:</p>
                <p><strong>{resetToken}</strong></p>
                <p>If you did not request this, please ignore this email.</p>
                <p>This token will expire in 1 hour.</p>
                """
        };

        using var client = new SmtpClient();

        await client.ConnectAsync(_settings.Host, _settings.Port, _settings.UseSsl, cancellationToken);
        await client.AuthenticateAsync(_settings.Username, _settings.Password, cancellationToken);
        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);

        logger.LogInformation("Password reset email sent to {Email}", toEmail);
    }
}
