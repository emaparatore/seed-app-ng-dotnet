using MediatR;
using Seed.Application.Common;
using Seed.Application.Common.Models;

namespace Seed.Application.Auth.Commands.RefreshToken;

public sealed record RefreshTokenCommand(string RefreshToken) : IRequest<Result<AuthResponse>>;
