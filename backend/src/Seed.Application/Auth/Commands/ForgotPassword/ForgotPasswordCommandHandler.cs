using MediatR;
using Microsoft.AspNetCore.Identity;
using Seed.Application.Common;
using Seed.Application.Common.Interfaces;
using Seed.Domain.Entities;

namespace Seed.Application.Auth.Commands.ForgotPassword;

public sealed class ForgotPasswordCommandHandler(
    UserManager<ApplicationUser> userManager,
    IEmailService emailService) : IRequestHandler<ForgotPasswordCommand, Result<string>>
{
    public async Task<Result<string>> Handle(ForgotPasswordCommand request, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByEmailAsync(request.Email);

        // Always return success to prevent email enumeration
        if (user is null || !user.IsActive)
            return Result<string>.Success("If an account with that email exists, a password reset link has been sent.");

        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        await emailService.SendPasswordResetEmailAsync(user.Email!, token, cancellationToken);

        return Result<string>.Success("If an account with that email exists, a password reset link has been sent.");
    }
}
