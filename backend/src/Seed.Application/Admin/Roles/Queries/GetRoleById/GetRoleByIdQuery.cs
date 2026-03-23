using MediatR;
using Seed.Application.Admin.Roles.Models;
using Seed.Application.Common;

namespace Seed.Application.Admin.Roles.Queries.GetRoleById;

public sealed record GetRoleByIdQuery(Guid RoleId) : IRequest<Result<AdminRoleDetailDto>>;
