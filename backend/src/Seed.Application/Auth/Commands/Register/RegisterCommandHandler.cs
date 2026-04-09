using System.Net;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Seed.Application.Common;
using Seed.Application.Common.Interfaces;
using Seed.Domain.Authorization;
using Seed.Domain.Entities;
using Seed.Shared.Configuration;

namespace Seed.Application.Auth.Commands.Register;

public sealed class RegisterCommandHandler(
    UserManager<ApplicationUser> userManager,
    IEmailService emailService,
    IOptions<ClientSettings> clientSettings,
    IOptions<PrivacySettings> privacySettings,
    IAuditService auditService) : IRequestHandler<RegisterCommand, Result<string>>
{
    private readonly ClientSettings _clientSettings = clientSettings.Value;
    private readonly PrivacySettings _privacySettings = privacySettings.Value;

    public async Task<Result<string>> Handle(RegisterCommand request, CancellationToken cancellationToken)
    {
        var existingUser = await userManager.FindByEmailAsync(request.Email);
        if (existingUser is not null)
            return Result<string>.Failure("A user with this email already exists.");

        var user = new ApplicationUser
        {
            Email = request.Email,
            UserName = request.Email,
            FirstName = request.FirstName,
            LastName = request.LastName,
            PrivacyPolicyAcceptedAt = DateTime.UtcNow,
            TermsAcceptedAt = DateTime.UtcNow,
            ConsentVersion = _privacySettings.ConsentVersion
        };

        var result = await userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
            return Result<string>.Failure(result.Errors.Select(e => e.Description).ToArray());

        await auditService.LogAsync(AuditActions.UserCreated, "User", user.Id.ToString(), $"Email: {request.Email}", user.Id, cancellationToken: cancellationToken);
        await auditService.LogAsync(AuditActions.ConsentGiven, "User", user.Id.ToString(), $"ConsentVersion: {_privacySettings.ConsentVersion}", user.Id, cancellationToken: cancellationToken);

        var token = await userManager.GenerateEmailConfirmationTokenAsync(user);
        var encodedToken = WebUtility.UrlEncode(token);
        var encodedEmail = WebUtility.UrlEncode(request.Email);
        var verificationLink = $"{_clientSettings.BaseUrl}/confirm-email?email={encodedEmail}&token={encodedToken}";

        await emailService.SendEmailVerificationAsync(request.Email, verificationLink, cancellationToken);

        return Result<string>.Success("Registration successful. Please check your email to verify your account.");
    }
}
