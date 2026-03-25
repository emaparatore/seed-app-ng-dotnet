using MediatR;
using Microsoft.AspNetCore.Identity;
using Seed.Application.Admin.Users.Models;
using Seed.Application.Common;
using Seed.Domain.Entities;

namespace Seed.Application.Admin.Users.Queries.GetUserById;

public sealed class GetUserByIdQueryHandler(
    UserManager<ApplicationUser> userManager)
    : IRequestHandler<GetUserByIdQuery, Result<AdminUserDetailDto>>
{
    public async Task<Result<AdminUserDetailDto>> Handle(
        GetUserByIdQuery request, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(request.UserId.ToString());
        if (user is null)
            return Result<AdminUserDetailDto>.Failure("User not found.");

        var roles = await userManager.GetRolesAsync(user);

        var dto = new AdminUserDetailDto(
            user.Id,
            user.Email!,
            user.FirstName,
            user.LastName,
            user.IsActive,
            roles.ToList(),
            user.CreatedAt,
            user.UpdatedAt,
            user.MustChangePassword,
            user.EmailConfirmed);

        return Result<AdminUserDetailDto>.Success(dto);
    }
}
