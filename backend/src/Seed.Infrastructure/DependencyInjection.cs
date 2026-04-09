using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Seed.Application.Common.Interfaces;
using Seed.Domain.Entities;
using Seed.Infrastructure.Persistence;
using Seed.Infrastructure.Persistence.Seeders;
using Seed.Infrastructure.Services;
using Seed.Shared.Configuration;
using Seed.Shared.Extensions;

namespace Seed.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));

        services.AddIdentity<ApplicationUser, ApplicationRole>(options =>
            {
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequiredLength = 8;
                options.User.RequireUniqueEmail = true;
            })
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();

        services.AddDistributedMemoryCache();

        services.Configure<ClientSettings>(configuration.GetSection(ClientSettings.SectionName));
        services.Configure<JwtSettings>(configuration.GetSection(JwtSettings.SectionName));
        services.Configure<PrivacySettings>(configuration.GetSection(PrivacySettings.SectionName));
        services.Configure<DataRetentionSettings>(configuration.GetSection(DataRetentionSettings.SectionName));
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IPermissionService, PermissionService>();
        services.AddScoped<ITokenBlacklistService, TokenBlacklistService>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<IUserPurgeService, UserPurgeService>();
        services.AddScoped<IDataCleanupService, DataCleanupService>();
        services.AddHostedService<DataRetentionBackgroundService>();
        services.AddScoped<IAuditLogReader, AuditLogReader>();
        services.AddScoped<ISystemSettingsService, SystemSettingsService>();
        services.Configure<SuperAdminSettings>(configuration.GetSection(SuperAdminSettings.SectionName));
        services.AddScoped<RolesAndPermissionsSeeder>();
        services.AddScoped<SuperAdminSeeder>();
        services.AddScoped<SystemSettingsSeeder>();

        services.Configure<ModulesSettings>(configuration.GetSection(ModulesSettings.SectionName));

        if (configuration.IsPaymentsModuleEnabled())
        {
            services.Configure<StripeSettings>(configuration.GetSection(StripeSettings.SectionName));
        }

        var smtpSection = configuration.GetSection(SmtpSettings.SectionName);
        if (smtpSection.Exists() && !string.IsNullOrWhiteSpace(smtpSection[nameof(SmtpSettings.Host)]))
        {
            services.Configure<SmtpSettings>(smtpSection);
            services.AddScoped<IEmailService, SmtpEmailService>();
        }
        else
        {
            services.AddScoped<IEmailService, ConsoleEmailService>();
        }

        return services;
    }
}
