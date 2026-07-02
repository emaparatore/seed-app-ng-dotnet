using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Seed.Domain.Authorization;
using Seed.Domain.Entities;

namespace Seed.Infrastructure.Persistence.Seeders;

public class SystemSettingsSeeder(
    ApplicationDbContext dbContext,
    ILogger<SystemSettingsSeeder> logger)
{
    private const string AppNameKey = "General.AppName";
    private const string PreviousDefaultAppName = "Starter App";

    public async Task SeedAsync()
    {
        var defaultAppName = SystemSettingsDefaults.GetAll()
            .Single(s => s.Key == AppNameKey)
            .Value;

        var existingAppName = await dbContext.SystemSettings
            .SingleOrDefaultAsync(s => s.Key == AppNameKey);

        if (existingAppName?.Value == PreviousDefaultAppName)
        {
            existingAppName.Value = defaultAppName;
            await dbContext.SaveChangesAsync();
            logger.LogInformation("Updated default application name to {AppName}", defaultAppName);
        }

        var existingKeys = await dbContext.SystemSettings
            .Select(s => s.Key)
            .ToHashSetAsync();

        var defaults = SystemSettingsDefaults.GetAll();
        var newSettings = defaults
            .Where(d => !existingKeys.Contains(d.Key))
            .Select(d => new SystemSetting
            {
                Key = d.Key,
                Value = d.Value,
                Type = d.Type,
                Category = d.Category,
                Description = d.Description
            })
            .ToList();

        if (newSettings.Count > 0)
        {
            dbContext.SystemSettings.AddRange(newSettings);
            await dbContext.SaveChangesAsync();
            logger.LogInformation("Seeded {Count} system settings", newSettings.Count);
        }
        else
        {
            logger.LogDebug("All system settings already exist, skipping");
        }
    }
}
