using MediatR;
using Seed.Application.Admin.Roles.Models;
using Seed.Application.Common;

namespace Seed.Application.Admin.Roles.Queries.GetPermissions;

public sealed record GetPermissionsQuery : IRequest<Result<IReadOnlyList<PermissionDto>>>;
