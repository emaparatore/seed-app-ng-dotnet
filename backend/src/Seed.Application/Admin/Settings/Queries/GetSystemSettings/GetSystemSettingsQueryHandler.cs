using MediatR;
using Seed.Application.Admin.Settings.Models;
using Seed.Application.Common;
using Seed.Application.Common.Interfaces;

namespace Seed.Application.Admin.Settings.Queries.GetSystemSettings;

public sealed class GetSystemSettingsQueryHandler(
    ISystemSettingsService settingsService)
    : IRequestHandler<GetSystemSettingsQuery, Result<IReadOnlyList<SystemSettingDto>>>
{
    public async Task<Result<IReadOnlyList<SystemSettingDto>>> Handle(
        GetSystemSettingsQuery request, CancellationToken cancellationToken)
    {
        var settings = await settingsService.GetAllAsync(cancellationToken);
        return Result<IReadOnlyList<SystemSettingDto>>.Success(settings);
    }
}
