using System.Net;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Seed.Application.Common;
using Seed.Application.Common.Interfaces;
using Seed.Domain.Authorization;
using Seed.Domain.Entities;
using Seed.Shared.Configuration;

namespace Seed.Application.Admin.Users.Commands.AdminResetPassword;

public sealed class AdminResetPasswordCommandHandler(
    UserManager<ApplicationUser> userManager,
    IEmailService emailService,
    IOptions<ClientSettings> clientSettings,
    IAuditService auditService)
    : IRequestHandler<AdminResetPasswordCommand, Result<bool>>
{
    private readonly ClientSettings _clientSettings = clientSettings.Value;

    public async Task<Result<bool>> Handle(AdminResetPasswordCommand request, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(request.UserId.ToString());
        if (user is null)
            return Result<bool>.Failure("User not found.");

        var resetToken = await userManager.GeneratePasswordResetTokenAsync(user);
        var encodedToken = WebUtility.UrlEncode(resetToken);
        var encodedEmail = WebUtility.UrlEncode(user.Email!);
        var resetLink = $"{_clientSettings.BaseUrl}/reset-password?email={encodedEmail}&token={encodedToken}";

        await emailService.SendPasswordResetEmailAsync(user.Email!, resetLink, cancellationToken);

        await auditService.LogAsync(
            AuditActions.PasswordResetRequested,
            "User",
            user.Id.ToString(),
            $"Admin-initiated password reset for {user.Email}",
            request.CurrentUserId,
            request.IpAddress,
            request.UserAgent,
            cancellationToken);

        return Result<bool>.Success(true);
    }
}
