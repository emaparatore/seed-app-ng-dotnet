using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Seed.Application.Common;
using Seed.Application.Common.Interfaces;
using Seed.Domain.Authorization;
using Seed.Domain.Entities;
using Seed.Shared.Configuration;

namespace Seed.Application.Auth.Commands.AcceptUpdatedConsent;

public sealed class AcceptUpdatedConsentCommandHandler(
    UserManager<ApplicationUser> userManager,
    IOptions<PrivacySettings> privacySettings,
    IAuditService auditService) : IRequestHandler<AcceptUpdatedConsentCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(AcceptUpdatedConsentCommand request, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(request.UserId.ToString());
        if (user is null)
            return Result<bool>.Failure("User not found.");

        var currentVersion = privacySettings.Value.ConsentVersion;

        user.PrivacyPolicyAcceptedAt = DateTime.UtcNow;
        user.TermsAcceptedAt = DateTime.UtcNow;
        user.ConsentVersion = currentVersion;

        var updateResult = await userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
            return Result<bool>.Failure(updateResult.Errors.Select(e => e.Description).ToArray());

        await auditService.LogAsync(
            AuditActions.ConsentGiven, "User", user.Id.ToString(),
            $"Consent re-accepted for version {currentVersion}", user.Id,
            cancellationToken: cancellationToken);

        return Result<bool>.Success(true);
    }
}
