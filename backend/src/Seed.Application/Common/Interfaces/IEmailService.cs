namespace Seed.Application.Common.Interfaces;

public interface IEmailService
{
    Task SendPasswordResetEmailAsync(string toEmail, string resetLink, CancellationToken cancellationToken = default);
    Task SendEmailVerificationAsync(string toEmail, string verificationLink, CancellationToken cancellationToken = default);
    Task SendSubscriptionConfirmationAsync(string toEmail, string planName, CancellationToken ct = default);
    Task SendTrialEndingNotificationAsync(string toEmail, string planName, int daysRemaining, CancellationToken ct = default);
    Task SendSubscriptionCanceledAsync(string toEmail, string planName, DateTime endDate, CancellationToken ct = default);
}
