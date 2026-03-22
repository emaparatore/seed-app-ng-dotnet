using System.Text.Json.Serialization;
using MediatR;
using Seed.Application.Admin.Users.Models;
using Seed.Application.Common;
using Seed.Application.Common.Models;

namespace Seed.Application.Admin.Users.Queries.GetUsers;

public sealed record GetUsersQuery : IRequest<Result<PagedResult<AdminUserDto>>>
{
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 10;
    public string? SearchTerm { get; init; }
    public string? RoleFilter { get; init; }
    public bool? StatusFilter { get; init; }
    public DateTime? DateFrom { get; init; }
    public DateTime? DateTo { get; init; }
    public string? SortBy { get; init; }
    public bool SortDescending { get; init; }
}
