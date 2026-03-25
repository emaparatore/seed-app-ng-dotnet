using MediatR;
using Seed.Application.Admin.Users.Models;
using Seed.Application.Common;

namespace Seed.Application.Admin.Users.Queries.GetUserById;

public sealed record GetUserByIdQuery(Guid UserId) : IRequest<Result<AdminUserDetailDto>>;
