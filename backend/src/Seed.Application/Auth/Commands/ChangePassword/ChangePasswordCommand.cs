using MediatR;
using Seed.Application.Common;

namespace Seed.Application.Auth.Commands.ChangePassword;

public sealed record ChangePasswordCommand(string UserId, string CurrentPassword, string NewPassword) : IRequest<Result<bool>>;
