using MediatR;
using Seed.Application.Common;

namespace Seed.Application.Auth.Commands.Register;

public sealed record RegisterCommand(
    string Email,
    string Password,
    string FirstName,
    string LastName,
    bool AcceptPrivacyPolicy,
    bool AcceptTermsOfService) : IRequest<Result<string>>;
