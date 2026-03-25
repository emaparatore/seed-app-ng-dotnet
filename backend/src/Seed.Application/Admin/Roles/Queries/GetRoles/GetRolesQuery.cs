using MediatR;
using Seed.Application.Admin.Roles.Models;
using Seed.Application.Common;

namespace Seed.Application.Admin.Roles.Queries.GetRoles;

public sealed record GetRolesQuery : IRequest<Result<IReadOnlyList<AdminRoleDto>>>;
