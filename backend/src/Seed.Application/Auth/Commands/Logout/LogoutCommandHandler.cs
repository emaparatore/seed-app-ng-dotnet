using MediatR;
using Seed.Application.Common;
using Seed.Application.Common.Interfaces;

namespace Seed.Application.Auth.Commands.Logout;

public sealed class LogoutCommandHandler(
    ITokenService tokenService) : IRequestHandler<LogoutCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(LogoutCommand request, CancellationToken cancellationToken)
    {
        await tokenService.RevokeTokenAsync(request.RefreshToken);
        return Result<bool>.Success(true);
    }
}
