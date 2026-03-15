using System.Net;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Seed.Application.Common;
using Seed.Application.Common.Interfaces;
using Seed.Domain.Entities;
using Seed.Shared.Configuration;

namespace Seed.Application.Auth.Commands.ForgotPassword;

public sealed class ForgotPasswordCommandHandler(
    UserManager<ApplicationUser> userManager,
    IEmailService emailService,
    IOptions<ClientSettings> clientSettings) : IRequestHandler<ForgotPasswordCommand, Result<string>>
{
    private readonly ClientSettings _clientSettings = clientSettings.Value;

    public async Task<Result<string>> Handle(ForgotPasswordCommand request, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByEmailAsync(request.Email);

        // Always return success to prevent email enumeration
        if (user is null || !user.IsActive)
            return Result<string>.Success("If an account with that email exists, a password reset link has been sent.");

        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        var encodedToken = WebUtility.UrlEncode(token);
        var encodedEmail = WebUtility.UrlEncode(user.Email!);
        var resetLink = $"{_clientSettings.BaseUrl}/reset-password?email={encodedEmail}&token={encodedToken}";

        await emailService.SendPasswordResetEmailAsync(user.Email!, resetLink, cancellationToken);

        return Result<string>.Success("If an account with that email exists, a password reset link has been sent.");
    }
}
