using MediatR;
using Microsoft.AspNetCore.Identity;
using Seed.Application.Common;
using Seed.Domain.Entities;

namespace Seed.Application.Auth.Commands.ResetPassword;

public sealed class ResetPasswordCommandHandler(
    UserManager<ApplicationUser> userManager) : IRequestHandler<ResetPasswordCommand, Result<string>>
{
    public async Task<Result<string>> Handle(ResetPasswordCommand request, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null || !user.IsActive)
            return Result<string>.Failure("Invalid or expired reset token.");

        var result = await userManager.ResetPasswordAsync(user, request.Token, request.NewPassword);
        if (!result.Succeeded)
        {
            var errors = result.Errors.Select(e => e.Description).ToArray();
            return Result<string>.Failure(errors);
        }

        return Result<string>.Success("Password has been reset successfully.");
    }
}
