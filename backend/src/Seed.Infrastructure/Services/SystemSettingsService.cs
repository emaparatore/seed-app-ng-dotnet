using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Seed.Application.Admin.Settings.Models;
using Seed.Application.Common;
using Seed.Application.Common.Interfaces;
using Seed.Domain.Authorization;
using Seed.Infrastructure.Persistence;

namespace Seed.Infrastructure.Services;

public sealed class SystemSettingsService(
    ApplicationDbContext dbContext,
    IDistributedCache cache,
    IAuditService auditService) : ISystemSettingsService
{
    private const string CacheKey = "system_settings:all";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    public async Task<IReadOnlyList<SystemSettingDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var cached = await cache.GetStringAsync(CacheKey, cancellationToken);
        if (cached is not null)
        {
            return JsonSerializer.Deserialize<List<SystemSettingDto>>(cached)!;
        }

        var settings = await dbContext.SystemSettings
            .OrderBy(s => s.Category)
            .ThenBy(s => s.Key)
            .Select(s => new SystemSettingDto(
                s.Key, s.Value, s.Type, s.Category, s.Description, s.ModifiedBy, s.ModifiedAt))
            .ToListAsync(cancellationToken);

        await cache.SetStringAsync(CacheKey, JsonSerializer.Serialize(settings), new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = CacheDuration
        }, cancellationToken);

        return settings;
    }

    public async Task<Result<bool>> UpdateAsync(
        IReadOnlyList<UpdateSettingItem> changes,
        Guid modifiedBy,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default)
    {
        var keys = changes.Select(c => c.Key).ToList();
        var existingSettings = await dbContext.SystemSettings
            .Where(s => keys.Contains(s.Key))
            .ToListAsync(cancellationToken);

        if (existingSettings.Count != keys.Count)
        {
            var missingKeys = keys.Except(existingSettings.Select(s => s.Key));
            return Result<bool>.Failure($"Unknown setting keys: {string.Join(", ", missingKeys)}");
        }

        var validationErrors = ValidateTypes(changes, existingSettings);
        if (validationErrors.Count > 0)
            return Result<bool>.Failure(validationErrors.ToArray());

        var auditDetails = new List<object>();

        foreach (var change in changes)
        {
            var setting = existingSettings.First(s => s.Key == change.Key);
            if (setting.Value == change.Value)
                continue;

            auditDetails.Add(new
            {
                key = setting.Key,
                before = setting.Value,
                after = change.Value
            });

            setting.Value = change.Value;
            setting.ModifiedBy = modifiedBy;
            setting.ModifiedAt = DateTime.UtcNow;
        }

        if (auditDetails.Count > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);

            await auditService.LogAsync(
                AuditActions.SettingsChanged,
                "SystemSetting",
                details: JsonSerializer.Serialize(auditDetails),
                userId: modifiedBy,
                ipAddress: ipAddress,
                userAgent: userAgent,
                cancellationToken: cancellationToken);
        }

        await cache.RemoveAsync(CacheKey, cancellationToken);

        return Result<bool>.Success(true);
    }

    public async Task<string?> GetValueAsync(string key, CancellationToken cancellationToken = default)
    {
        var all = await GetAllAsync(cancellationToken);
        return all.FirstOrDefault(s => s.Key == key)?.Value;
    }

    private static List<string> ValidateTypes(
        IReadOnlyList<UpdateSettingItem> changes,
        List<Domain.Entities.SystemSetting> existingSettings)
    {
        var errors = new List<string>();
        foreach (var change in changes)
        {
            var setting = existingSettings.First(s => s.Key == change.Key);
            switch (setting.Type)
            {
                case "bool" when change.Value is not "true" and not "false":
                    errors.Add($"Setting '{change.Key}' must be 'true' or 'false'.");
                    break;
                case "int" when !int.TryParse(change.Value, out _):
                    errors.Add($"Setting '{change.Key}' must be a valid integer.");
                    break;
            }
        }
        return errors;
    }
}
