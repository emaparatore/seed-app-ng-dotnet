using MediatR;
using Seed.Application.Common;
using Seed.Application.Common.Models;

namespace Seed.Application.Auth.Queries.GetCurrentUser;

public sealed record GetCurrentUserQuery(Guid UserId) : IRequest<Result<UserDto>>;
