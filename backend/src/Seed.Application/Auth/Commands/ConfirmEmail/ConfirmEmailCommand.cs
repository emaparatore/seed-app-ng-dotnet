using MediatR;
using Seed.Application.Common;
using Seed.Application.Common.Models;

namespace Seed.Application.Auth.Commands.ConfirmEmail;

public sealed record ConfirmEmailCommand(string Email, string Token) : IRequest<Result<AuthResponse>>;
