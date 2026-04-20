using MediatR;
using Microsoft.AspNetCore.Identity;
using Seed.Application.Common;
using Seed.Application.Common.Interfaces;
using Seed.Domain.Entities;

namespace Seed.Application.Auth.Queries.GetCurrentUser;

public sealed class GetCurrentUserQueryHandler(
    UserManager<ApplicationUser> userManager,
    IPermissionService permissionService,
    ISubscriptionInfoService subscriptionInfoService) : IRequestHandler<GetCurrentUserQuery, Result<MeResponse>>
{
    public async Task<Result<MeResponse>> Handle(GetCurrentUserQuery request, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(request.UserId.ToString());
        if (user is null || !user.IsActive)
            return Result<MeResponse>.Failure("User not found.");

        var roles = await userManager.GetRolesAsync(user);
        var permissions = await permissionService.GetPermissionsAsync(user.Id);
        var subscription = await subscriptionInfoService.GetUserSubscriptionInfoAsync(user.Id, cancellationToken);

        return Result<MeResponse>.Success(
            new MeResponse(user.Id, user.Email!, user.FirstName, user.LastName, roles.ToList().AsReadOnly(), permissions.ToList().AsReadOnly(), subscription));
    }
}
