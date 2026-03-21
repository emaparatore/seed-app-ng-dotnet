using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Seed.Domain.Authorization;
using Seed.Domain.Entities;
using Seed.Shared.Configuration;

namespace Seed.Infrastructure.Persistence.Seeders;

public class SuperAdminSeeder
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SuperAdminSettings _settings;
    private readonly ILogger<SuperAdminSeeder> _logger;

    public SuperAdminSeeder(
        UserManager<ApplicationUser> userManager,
        IOptions<SuperAdminSettings> settings,
        ILogger<SuperAdminSeeder> logger)
    {
        _userManager = userManager;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task SeedAsync()
    {
        if (string.IsNullOrWhiteSpace(_settings.Email))
        {
            _logger.LogWarning("SuperAdmin email not configured. Skipping SuperAdmin seeding. " +
                               "Set SuperAdmin__Email and SuperAdmin__Password environment variables to create a SuperAdmin user");
            return;
        }

        if (string.IsNullOrWhiteSpace(_settings.Password))
        {
            _logger.LogWarning("SuperAdmin password not configured. Skipping SuperAdmin seeding. " +
                               "Set SuperAdmin__Password environment variable");
            return;
        }

        var usersInRole = await _userManager.GetUsersInRoleAsync(SystemRoles.SuperAdmin);
        if (usersInRole.Count > 0)
        {
            _logger.LogDebug("A SuperAdmin user already exists, skipping seeding");
            return;
        }

        var user = new ApplicationUser
        {
            UserName = _settings.Email,
            Email = _settings.Email,
            FirstName = _settings.FirstName,
            LastName = _settings.LastName,
            EmailConfirmed = true,
            IsActive = true,
            MustChangePassword = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var createResult = await _userManager.CreateAsync(user, _settings.Password);
        if (!createResult.Succeeded)
        {
            _logger.LogWarning("Failed to create SuperAdmin user: {Errors}",
                string.Join(", ", createResult.Errors.Select(e => e.Description)));
            return;
        }

        var roleResult = await _userManager.AddToRoleAsync(user, SystemRoles.SuperAdmin);
        if (!roleResult.Succeeded)
        {
            _logger.LogWarning("Failed to assign SuperAdmin role: {Errors}",
                string.Join(", ", roleResult.Errors.Select(e => e.Description)));
            return;
        }

        _logger.LogInformation("SuperAdmin user created successfully with email {Email}", _settings.Email);
    }
}
