using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Seed.Domain.Authorization;
using Seed.Domain.Entities;

namespace Seed.Infrastructure.Persistence.Seeders;

public class SystemSettingsSeeder(
    ApplicationDbContext dbContext,
    ILogger<SystemSettingsSeeder> logger)
{
    public async Task SeedAsync()
    {
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
