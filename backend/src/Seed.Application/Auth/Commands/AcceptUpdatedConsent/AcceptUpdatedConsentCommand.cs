using MediatR;
using Seed.Application.Common;

namespace Seed.Application.Auth.Commands.AcceptUpdatedConsent;

public sealed record AcceptUpdatedConsentCommand(Guid UserId) : IRequest<Result<bool>>;
