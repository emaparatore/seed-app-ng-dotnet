using MediatR;
using Seed.Application.Common;

namespace Seed.Application.Auth.Queries.GetCurrentUser;

public sealed record GetCurrentUserQuery(Guid UserId) : IRequest<Result<MeResponse>>;
