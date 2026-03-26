using MediatR;
using Seed.Application.Common;
using Seed.Application.Common.Interfaces;

namespace Seed.Application.Admin.Settings.Commands.UpdateSystemSettings;

public sealed class UpdateSystemSettingsCommandHandler(
    ISystemSettingsService settingsService)
    : IRequestHandler<UpdateSystemSettingsCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(
        UpdateSystemSettingsCommand request, CancellationToken cancellationToken)
    {
        return await settingsService.UpdateAsync(
            request.Items,
            request.CurrentUserId,
            request.IpAddress,
            request.UserAgent,
            cancellationToken);
    }
}
