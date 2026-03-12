namespace Seed.Application.Common.Interfaces;

public interface IEmailService
{
    Task SendPasswordResetEmailAsync(string toEmail, string resetToken, CancellationToken cancellationToken = default);
}
