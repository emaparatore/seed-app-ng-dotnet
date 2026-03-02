using MediatR;
using Seed.Application.Common;

namespace Seed.Application.Auth.Commands.Logout;

public sealed record LogoutCommand(string RefreshToken) : IRequest<Result<bool>>;
