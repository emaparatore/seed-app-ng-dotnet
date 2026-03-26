using MediatR;
using Microsoft.AspNetCore.Identity;
using Seed.Application.Admin.Users.Models;
using Seed.Application.Common;
using Seed.Application.Common.Models;
using Seed.Domain.Entities;

namespace Seed.Application.Admin.Users.Queries.GetUsers;

public sealed class GetUsersQueryHandler(
    UserManager<ApplicationUser> userManager)
    : IRequestHandler<GetUsersQuery, Result<PagedResult<AdminUserDto>>>
{
    public async Task<Result<PagedResult<AdminUserDto>>> Handle(
        GetUsersQuery request, CancellationToken cancellationToken)
    {
        var query = userManager.Users.AsQueryable();

        // Search by name or email
        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.ToLower();
            query = query.Where(u =>
                u.Email!.ToLower().Contains(term) ||
                u.FirstName.ToLower().Contains(term) ||
                u.LastName.ToLower().Contains(term));
        }

        // Filter by status
        if (request.StatusFilter.HasValue)
            query = query.Where(u => u.IsActive == request.StatusFilter.Value);

        // Filter by registration date
        if (request.DateFrom.HasValue)
            query = query.Where(u => u.CreatedAt >= request.DateFrom.Value);
        if (request.DateTo.HasValue)
            query = query.Where(u => u.CreatedAt <= request.DateTo.Value);

        // Sorting
        query = request.SortBy?.ToLower() switch
        {
            "email" => request.SortDescending ? query.OrderByDescending(u => u.Email) : query.OrderBy(u => u.Email),
            "firstname" => request.SortDescending ? query.OrderByDescending(u => u.FirstName) : query.OrderBy(u => u.FirstName),
            "lastname" => request.SortDescending ? query.OrderByDescending(u => u.LastName) : query.OrderBy(u => u.LastName),
            "isactive" => request.SortDescending ? query.OrderByDescending(u => u.IsActive) : query.OrderBy(u => u.IsActive),
            "createdat" => request.SortDescending ? query.OrderByDescending(u => u.CreatedAt) : query.OrderBy(u => u.CreatedAt),
            _ => query.OrderByDescending(u => u.CreatedAt)
        };

        // If role filter is applied, we need to get users in that role first
        if (!string.IsNullOrWhiteSpace(request.RoleFilter))
        {
            var usersInRole = await userManager.GetUsersInRoleAsync(request.RoleFilter);
            var userIdsInRole = usersInRole.Select(u => u.Id).ToHashSet();
            query = query.Where(u => userIdsInRole.Contains(u.Id));
        }

        // Get total count
        var allUsers = query.ToList();
        var totalCount = allUsers.Count;

        // Pagination
        var pagedUsers = allUsers
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToList();

        // Build DTOs with roles
        var items = new List<AdminUserDto>();
        foreach (var user in pagedUsers)
        {
            var roles = await userManager.GetRolesAsync(user);
            items.Add(new AdminUserDto(
                user.Id,
                user.Email!,
                user.FirstName,
                user.LastName,
                user.IsActive,
                roles.ToList(),
                user.CreatedAt));
        }

        var pagedResult = new PagedResult<AdminUserDto>(items, request.PageNumber, request.PageSize, totalCount);
        return Result<PagedResult<AdminUserDto>>.Success(pagedResult);
    }
}
