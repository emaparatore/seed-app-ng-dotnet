using MediatR;
using Seed.Application.Common;
using Seed.Application.Common.Models;

namespace Seed.Application.Auth.Commands.Login;

public sealed record LoginCommand(string Email, string Password) : IRequest<Result<AuthResponse>>;
