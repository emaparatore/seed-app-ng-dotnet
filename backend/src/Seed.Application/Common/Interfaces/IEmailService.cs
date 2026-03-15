namespace Seed.Application.Common.Interfaces;

public interface IEmailService
{
    Task SendPasswordResetEmailAsync(string toEmail, string resetToken, CancellationToken cancellationToken = default);
    Task SendEmailVerificationAsync(string toEmail, string verificationLink, CancellationToken cancellationToken = default);
}
