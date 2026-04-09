using MediatR;
using Microsoft.AspNetCore.Identity;
using Seed.Application.Common;
using Seed.Application.Common.Interfaces;
using Seed.Domain.Authorization;
using Seed.Domain.Entities;

namespace Seed.Application.Auth.Queries.ExportMyData;

public sealed class ExportMyDataQueryHandler(
    UserManager<ApplicationUser> userManager,
    IAuditLogReader auditLogReader,
    IAuditService auditService) : IRequestHandler<ExportMyDataQuery, Result<UserDataExportDto>>
{
    public async Task<Result<UserDataExportDto>> Handle(ExportMyDataQuery request, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(request.UserId.ToString());
        if (user is null)
            return Result<UserDataExportDto>.Failure("User not found.");

        var roles = await userManager.GetRolesAsync(user);

        var auditEntries = auditLogReader.GetQueryable()
            .Where(a => a.UserId == request.UserId)
            .OrderByDescending(a => a.Timestamp)
            .Select(a => new AuditLogExportDto(
                a.Timestamp,
                a.Action,
                a.EntityType,
                a.EntityId,
                a.Details,
                a.IpAddress))
            .ToList();

        var profile = new UserProfileExportDto(
            user.Id,
            user.Email!,
            user.FirstName,
            user.LastName,
            user.CreatedAt,
            user.UpdatedAt,
            user.IsActive);

        var consent = new UserConsentExportDto(
            user.PrivacyPolicyAcceptedAt,
            user.TermsAcceptedAt,
            user.ConsentVersion);

        var exportDto = new UserDataExportDto(profile, consent, roles.ToList().AsReadOnly(), auditEntries.AsReadOnly());

        await auditService.LogAsync(
            AuditActions.DataExported,
            "User",
            request.UserId.ToString(),
            "User data exported",
            request.UserId,
            cancellationToken: cancellationToken);

        return Result<UserDataExportDto>.Success(exportDto);
    }
}
