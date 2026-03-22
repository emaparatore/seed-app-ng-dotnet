using MediatR;
using Microsoft.AspNetCore.Identity;
using Seed.Application.Common;
using Seed.Domain.Entities;

namespace Seed.Application.Auth.Commands.ChangePassword;

public sealed class ChangePasswordCommandHandler(
    UserManager<ApplicationUser> userManager) : IRequestHandler<ChangePasswordCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(ChangePasswordCommand request, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(request.UserId);
        if (user is null || !user.IsActive)
            return Result<bool>.Failure("User not found.");

        var validPassword = await userManager.CheckPasswordAsync(user, request.CurrentPassword);
        if (!validPassword)
            return Result<bool>.Failure("Current password is incorrect.");

        var changeResult = await userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!changeResult.Succeeded)
            return Result<bool>.Failure(changeResult.Errors.Select(e => e.Description).ToArray());

        user.MustChangePassword = false;
        user.UpdatedAt = DateTime.UtcNow;
        await userManager.UpdateAsync(user);

        return Result<bool>.Success(true);
    }
}
