using MediatR;
using Seed.Application.Common;
using Seed.Application.Common.Models;

namespace Seed.Application.Auth.Commands.Register;

public sealed record RegisterCommand(
    string Email,
    string Password,
    string FirstName,
    string LastName) : IRequest<Result<AuthResponse>>;
