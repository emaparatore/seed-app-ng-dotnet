using System.Net;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Seed.Application.Common;
using Seed.Application.Common.Interfaces;
using Seed.Domain.Authorization;
using Seed.Domain.Entities;
using Seed.Shared.Configuration;

namespace Seed.Application.Auth.Commands.ResendConfirmationEmail;

public sealed class ResendConfirmationEmailCommandHandler(
    UserManager<ApplicationUser> userManager,
    IEmailService emailService,
    IOptions<ClientSettings> clientSettings,
    IAuditService auditService) : IRequestHandler<ResendConfirmationEmailCommand, Result<string>>
{
    private readonly ClientSettings _clientSettings = clientSettings.Value;

    public async Task<Result<string>> Handle(ResendConfirmationEmailCommand request, CancellationToken cancellationToken)
    {
        await auditService.LogAsync(AuditActions.ConfirmationEmailResent, "User", details: $"Email: {request.Email}", cancellationToken: cancellationToken);

        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null || !user.IsActive)
            return Result<string>.Success("If an account with that email exists and requires verification, a new confirmation link has been sent.");

        if (user.EmailConfirmed)
            return Result<string>.Success("If an account with that email exists and requires verification, a new confirmation link has been sent.");

        var token = await userManager.GenerateEmailConfirmationTokenAsync(user);
        var encodedToken = WebUtility.UrlEncode(token);
        var encodedEmail = WebUtility.UrlEncode(user.Email!);
        var verificationLink = $"{_clientSettings.BaseUrl}/confirm-email?email={encodedEmail}&token={encodedToken}";

        await emailService.SendEmailVerificationAsync(user.Email!, verificationLink, cancellationToken);

        return Result<string>.Success("If an account with that email exists and requires verification, a new confirmation link has been sent.");
    }
}
