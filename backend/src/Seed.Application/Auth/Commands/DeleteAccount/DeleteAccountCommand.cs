using MediatR;
using Seed.Application.Common;

namespace Seed.Application.Auth.Commands.DeleteAccount;

public sealed record DeleteAccountCommand(Guid UserId, string Password) : IRequest<Result<bool>>;
