using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MediatR;
using Seed.Application.Admin.InvoiceRequests.Commands.UpdateInvoiceRequestStatus;
using Seed.Application.Admin.InvoiceRequests.Models;
using Seed.Application.Admin.InvoiceRequests.Queries.GetInvoiceRequests;
using Seed.Application.Admin.Plans.Commands.ArchivePlan;
using Seed.Application.Admin.Plans.Commands.CreatePlan;
using Seed.Application.Admin.Plans.Commands.UpdatePlan;
using Seed.Application.Admin.Plans.Models;
using Seed.Application.Admin.Plans.Queries.GetAdminPlanById;
using Seed.Application.Admin.Plans.Queries.GetAdminPlans;
using Seed.Application.Admin.Subscriptions.Models;
using Seed.Application.Admin.Subscriptions.Queries.GetSubscriptionDetail;
using Seed.Application.Admin.Subscriptions.Queries.GetSubscriptionMetrics;
using Seed.Application.Admin.Subscriptions.Queries.GetSubscriptionsList;
using Seed.Application.Billing.Commands.CreateCheckoutSession;
using Seed.Application.Billing.Commands.CreateInvoiceRequest;
using Seed.Application.Billing.Commands.CreatePortalSession;
using Seed.Application.Billing.Commands.CancelSubscription;
using Seed.Application.Billing.Models;
using Seed.Application.Billing.Queries.GetMyInvoiceRequests;
using Seed.Application.Billing.Queries.GetMySubscription;
using Seed.Application.Billing.Queries.GetPlans;
using Seed.Application.Common;
using Seed.Application.Common.Interfaces;
using Seed.Application.Common.Models;
using Seed.Domain.Entities;
using Seed.Infrastructure.Billing.Commands;
using Seed.Infrastructure.Billing.Queries;
using Seed.Infrastructure.Billing.Services;
using Seed.Infrastructure.Persistence;
using Seed.Infrastructure.Persistence.Seeders;
using Seed.Infrastructure.Services;
using Seed.Infrastructure.Services.Payments;
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
            services.AddScoped<ISubscriptionAccessService, SubscriptionAccessService>();
            services.AddScoped<ISubscriptionInfoService, SubscriptionInfoService>();

            services.Configure<StripeSettings>(configuration.GetSection(StripeSettings.SectionName));

            var stripeSection = configuration.GetSection(StripeSettings.SectionName);
            var provider = configuration.GetValue<string>("Modules:Payments:Provider");
            var secretKey = stripeSection[nameof(StripeSettings.SecretKey)];

            if (!string.Equals(provider, "Stripe", StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrWhiteSpace(secretKey))
            {
                services.AddScoped<IPaymentGateway, MockPaymentGateway>();
            }
            else
            {
                services.AddScoped<IPaymentGateway, StripePaymentGateway>();
            }

            services.AddMemoryCache();
            services.AddScoped<IWebhookEventHandler, StripeWebhookEventHandler>();
            services.AddScoped<IRequestHandler<GetPlansQuery, Result<IReadOnlyList<PlanDto>>>, GetPlansQueryHandler>();
            services.AddScoped<IRequestHandler<CreateCheckoutSessionCommand, Result<CheckoutSessionResponse>>, CreateCheckoutSessionCommandHandler>();
            services.AddScoped<IRequestHandler<GetMySubscriptionQuery, Result<UserSubscriptionDto?>>, GetMySubscriptionQueryHandler>();
            services.AddScoped<IRequestHandler<CreatePortalSessionCommand, Result<PortalSessionResponse>>, CreatePortalSessionCommandHandler>();
            services.AddScoped<IRequestHandler<CancelSubscriptionCommand, Result<bool>>, CancelSubscriptionCommandHandler>();

            services.AddScoped<IRequestHandler<CreatePlanCommand, Result<Guid>>, CreatePlanCommandHandler>();
            services.AddScoped<IRequestHandler<UpdatePlanCommand, Result<bool>>, UpdatePlanCommandHandler>();
            services.AddScoped<IRequestHandler<ArchivePlanCommand, Result<bool>>, ArchivePlanCommandHandler>();
            services.AddScoped<IRequestHandler<GetAdminPlansQuery, Result<IReadOnlyList<AdminPlanDto>>>, GetAdminPlansQueryHandler>();
            services.AddScoped<IRequestHandler<GetAdminPlanByIdQuery, Result<AdminPlanDto>>, GetAdminPlanByIdQueryHandler>();

            services.AddScoped<IRequestHandler<GetSubscriptionMetricsQuery, Result<SubscriptionMetricsDto>>, GetSubscriptionMetricsQueryHandler>();
            services.AddScoped<IRequestHandler<GetSubscriptionsListQuery, Result<PagedResult<AdminSubscriptionDto>>>, GetSubscriptionsListQueryHandler>();
            services.AddScoped<IRequestHandler<GetSubscriptionDetailQuery, Result<AdminSubscriptionDetailDto>>, GetSubscriptionDetailQueryHandler>();

            services.AddScoped<IRequestHandler<CreateInvoiceRequestCommand, Result<Guid>>, CreateInvoiceRequestCommandHandler>();
            services.AddScoped<IRequestHandler<GetMyInvoiceRequestsQuery, Result<IReadOnlyList<InvoiceRequestDto>>>, GetMyInvoiceRequestsQueryHandler>();
            services.AddScoped<IRequestHandler<GetInvoiceRequestsQuery, Result<PagedResult<AdminInvoiceRequestDto>>>, GetAdminInvoiceRequestsQueryHandler>();
            services.AddScoped<IRequestHandler<UpdateInvoiceRequestStatusCommand, Result<bool>>, UpdateInvoiceRequestStatusCommandHandler>();
        }
        else
        {
            services.AddScoped<ISubscriptionAccessService, AlwaysAllowSubscriptionAccessService>();
            services.AddScoped<ISubscriptionInfoService, NullSubscriptionInfoService>();
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
