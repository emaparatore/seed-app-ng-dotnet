using Seed.Application.Admin.Settings.Models;
using Seed.Application.Common;

namespace Seed.Application.Common.Interfaces;

public interface ISystemSettingsService
{
    Task<IReadOnlyList<SystemSettingDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<Result<bool>> UpdateAsync(IReadOnlyList<UpdateSettingItem> changes, Guid modifiedBy, string? ipAddress, string? userAgent, CancellationToken cancellationToken = default);
    Task<string?> GetValueAsync(string key, CancellationToken cancellationToken = default);
}
