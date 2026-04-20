using MailKit.Net.Smtp;
using MailKit.Security;
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

    public async Task SendPasswordResetEmailAsync(string toEmail, string resetLink, CancellationToken cancellationToken = default)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_settings.FromName, _settings.FromEmail));
        message.To.Add(MailboxAddress.Parse(toEmail));
        message.Subject = "Password Reset Request";
        message.Body = new TextPart("html")
        {
            Text = $"""
                <h2>Password Reset</h2>
                <p>You requested a password reset. Click the button below to set a new password:</p>
                <p><a href="{resetLink}" style="display:inline-block;padding:12px 24px;background-color:#1976d2;color:#ffffff;text-decoration:none;border-radius:4px;">Reset Password</a></p>
                <p>Or copy and paste this link into your browser:</p>
                <p>{resetLink}</p>
                <p>If you did not request this, please ignore this email.</p>
                <p>This link will expire in 1 hour.</p>
                """
        };

        var socketOptions = Enum.Parse<SecureSocketOptions>(_settings.Security, ignoreCase: true);

        using var client = new SmtpClient();

        await client.ConnectAsync(_settings.Host, _settings.Port, socketOptions, cancellationToken);

        if (!string.IsNullOrEmpty(_settings.Username))
            await client.AuthenticateAsync(_settings.Username, _settings.Password, cancellationToken);

        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);

        logger.LogInformation("Password reset email sent to {Email}", toEmail);
    }

    public async Task SendEmailVerificationAsync(string toEmail, string verificationLink, CancellationToken cancellationToken = default)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_settings.FromName, _settings.FromEmail));
        message.To.Add(MailboxAddress.Parse(toEmail));
        message.Subject = "Verify your email address";
        message.Body = new TextPart("html")
        {
            Text = $"""
                <h2>Welcome!</h2>
                <p>Thank you for registering. Please verify your email address to activate your account.</p>
                <p><a href="{verificationLink}" style="display:inline-block;padding:12px 24px;background-color:#1976d2;color:#ffffff;text-decoration:none;border-radius:4px;">Verify Email Address</a></p>
                <p>Or copy and paste this link into your browser:</p>
                <p>{verificationLink}</p>
                <p>If you did not create an account, please ignore this email.</p>
                """
        };

        var socketOptions = Enum.Parse<SecureSocketOptions>(_settings.Security, ignoreCase: true);

        using var client = new SmtpClient();

        await client.ConnectAsync(_settings.Host, _settings.Port, socketOptions, cancellationToken);

        if (!string.IsNullOrEmpty(_settings.Username))
            await client.AuthenticateAsync(_settings.Username, _settings.Password, cancellationToken);

        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);

        logger.LogInformation("Email verification link sent to {Email}", toEmail);
    }

    public async Task SendSubscriptionConfirmationAsync(string toEmail, string planName, CancellationToken ct = default)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_settings.FromName, _settings.FromEmail));
        message.To.Add(MailboxAddress.Parse(toEmail));
        message.Subject = "Subscription Confirmed";
        message.Body = new TextPart("html")
        {
            Text = $"""
                <h2>Subscription Confirmed</h2>
                <p>Thank you! Your subscription to the <strong>{planName}</strong> plan has been activated.</p>
                <p>You can manage your subscription from your account settings.</p>
                """
        };

        var socketOptions = Enum.Parse<SecureSocketOptions>(_settings.Security, ignoreCase: true);

        using var client = new SmtpClient();
        await client.ConnectAsync(_settings.Host, _settings.Port, socketOptions, ct);
        if (!string.IsNullOrEmpty(_settings.Username))
            await client.AuthenticateAsync(_settings.Username, _settings.Password, ct);
        await client.SendAsync(message, ct);
        await client.DisconnectAsync(true, ct);

        logger.LogInformation("Subscription confirmation email sent to {Email}", toEmail);
    }

    public async Task SendTrialEndingNotificationAsync(string toEmail, string planName, int daysRemaining, CancellationToken ct = default)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_settings.FromName, _settings.FromEmail));
        message.To.Add(MailboxAddress.Parse(toEmail));
        message.Subject = $"Your trial ends in {daysRemaining} day(s)";
        message.Body = new TextPart("html")
        {
            Text = $"""
                <h2>Trial Ending Soon</h2>
                <p>Your trial for the <strong>{planName}</strong> plan will end in <strong>{daysRemaining} day(s)</strong>.</p>
                <p>To continue using all features, please ensure your payment method is up to date in your account settings.</p>
                """
        };

        var socketOptions = Enum.Parse<SecureSocketOptions>(_settings.Security, ignoreCase: true);

        using var client = new SmtpClient();
        await client.ConnectAsync(_settings.Host, _settings.Port, socketOptions, ct);
        if (!string.IsNullOrEmpty(_settings.Username))
            await client.AuthenticateAsync(_settings.Username, _settings.Password, ct);
        await client.SendAsync(message, ct);
        await client.DisconnectAsync(true, ct);

        logger.LogInformation("Trial ending notification sent to {Email} ({DaysRemaining} days remaining)", toEmail, daysRemaining);
    }

    public async Task SendSubscriptionCanceledAsync(string toEmail, string planName, DateTime endDate, CancellationToken ct = default)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_settings.FromName, _settings.FromEmail));
        message.To.Add(MailboxAddress.Parse(toEmail));
        message.Subject = "Subscription Canceled";
        message.Body = new TextPart("html")
        {
            Text = $"""
                <h2>Subscription Canceled</h2>
                <p>Your subscription to the <strong>{planName}</strong> plan has been canceled.</p>
                <p>You will continue to have access until <strong>{endDate:MMMM d, yyyy}</strong>.</p>
                <p>You can resubscribe at any time from your account settings.</p>
                """
        };

        var socketOptions = Enum.Parse<SecureSocketOptions>(_settings.Security, ignoreCase: true);

        using var client = new SmtpClient();
        await client.ConnectAsync(_settings.Host, _settings.Port, socketOptions, ct);
        if (!string.IsNullOrEmpty(_settings.Username))
            await client.AuthenticateAsync(_settings.Username, _settings.Password, ct);
        await client.SendAsync(message, ct);
        await client.DisconnectAsync(true, ct);

        logger.LogInformation("Subscription canceled email sent to {Email}", toEmail);
    }
}
