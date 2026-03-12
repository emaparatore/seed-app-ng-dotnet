using MediatR;
using Seed.Application.Common;

namespace Seed.Application.Auth.Commands.ResetPassword;

public sealed record ResetPasswordCommand(string Email, string Token, string NewPassword) : IRequest<Result<string>>;
