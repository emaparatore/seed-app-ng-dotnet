using MediatR;
using Seed.Application.Admin.Plans.Commands.CreatePlan;
using Seed.Application.Common;
using Seed.Application.Common.Interfaces;
using Seed.Application.Common.Models;
using Seed.Domain.Authorization;
using Seed.Domain.Entities;
using Seed.Infrastructure.Persistence;

namespace Seed.Infrastructure.Billing.Commands;

public sealed class CreatePlanCommandHandler(
    ApplicationDbContext dbContext,
    IPaymentGateway paymentGateway,
    IAuditService auditService)
    : IRequestHandler<CreatePlanCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(
        CreatePlanCommand request, CancellationToken cancellationToken)
    {
        var plan = new SubscriptionPlan
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Description = request.Description,
            MonthlyPrice = request.MonthlyPrice,
            YearlyPrice = request.YearlyPrice,
            TrialDays = request.TrialDays,
            IsFreeTier = request.IsFreeTier,
            IsDefault = request.IsDefault,
            IsPopular = request.IsPopular,
            SortOrder = request.SortOrder,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        foreach (var f in request.Features)
        {
            plan.Features.Add(new PlanFeature
            {
                Id = Guid.NewGuid(),
                Key = f.Key,
                Description = f.Description,
                LimitValue = f.LimitValue,
                SortOrder = f.SortOrder
            });
        }

        dbContext.SubscriptionPlans.Add(plan);
        await dbContext.SaveChangesAsync(cancellationToken);

        var syncResult = await paymentGateway.SyncPlanToProviderAsync(new SyncPlanRequest(
            ProductId: null,
            Name: plan.Name,
            Description: plan.Description,
            MonthlyPriceInCents: (long)(plan.MonthlyPrice * 100),
            YearlyPriceInCents: (long)(plan.YearlyPrice * 100),
            ExistingMonthlyPriceId: null,
            ExistingYearlyPriceId: null), cancellationToken);

        plan.StripeProductId = syncResult.ProductId;
        plan.StripePriceIdMonthly = syncResult.MonthlyPriceId;
        plan.StripePriceIdYearly = syncResult.YearlyPriceId;
        plan.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditService.LogAsync(
            AuditActions.PlanCreated,
            entityType: "SubscriptionPlan",
            entityId: plan.Id.ToString(),
            details: $"Plan '{plan.Name}' created",
            userId: request.CurrentUserId,
            ipAddress: request.IpAddress,
            userAgent: request.UserAgent,
            cancellationToken: cancellationToken);

        return Result<Guid>.Success(plan.Id);
    }
}
