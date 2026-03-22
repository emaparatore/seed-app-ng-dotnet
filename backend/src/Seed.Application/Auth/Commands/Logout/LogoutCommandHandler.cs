using MediatR;
using Seed.Application.Common;
using Seed.Application.Common.Interfaces;
using Seed.Domain.Authorization;

namespace Seed.Application.Auth.Commands.Logout;

public sealed class LogoutCommandHandler(
    ITokenService tokenService,
    IAuditService auditService) : IRequestHandler<LogoutCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(LogoutCommand request, CancellationToken cancellationToken)
    {
        await tokenService.RevokeTokenAsync(request.RefreshToken);
        await auditService.LogAsync(AuditActions.Logout, "User", request.UserId?.ToString(), userId: request.UserId, cancellationToken: cancellationToken);
        return Result<bool>.Success(true);
    }
}
