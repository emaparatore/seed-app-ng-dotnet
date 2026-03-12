using MediatR;
using Seed.Application.Common;

namespace Seed.Application.Auth.Commands.ForgotPassword;

public sealed record ForgotPasswordCommand(string Email) : IRequest<Result<string>>;
