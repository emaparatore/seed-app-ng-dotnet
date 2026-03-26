using MediatR;
using Seed.Application.Common;

namespace Seed.Application.Auth.Commands.ResendConfirmationEmail;

public sealed record ResendConfirmationEmailCommand(string Email) : IRequest<Result<string>>;
