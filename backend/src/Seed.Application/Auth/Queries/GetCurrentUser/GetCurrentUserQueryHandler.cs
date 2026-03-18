using MediatR;
using Microsoft.AspNetCore.Identity;
using Seed.Application.Common;
using Seed.Application.Common.Models;
using Seed.Domain.Entities;

namespace Seed.Application.Auth.Queries.GetCurrentUser;

public sealed class GetCurrentUserQueryHandler(
    UserManager<ApplicationUser> userManager) : IRequestHandler<GetCurrentUserQuery, Result<UserDto>>
{
    public async Task<Result<UserDto>> Handle(GetCurrentUserQuery request, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(request.UserId.ToString());
        if (user is null || !user.IsActive)
            return Result<UserDto>.Failure("User not found.");

        var roles = await userManager.GetRolesAsync(user);

        return Result<UserDto>.Success(
            new UserDto(user.Id, user.Email!, user.FirstName, user.LastName, roles.ToList().AsReadOnly()));
    }
}
