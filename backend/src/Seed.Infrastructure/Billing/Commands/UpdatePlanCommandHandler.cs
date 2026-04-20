using MediatR;
using Microsoft.EntityFrameworkCore;
using Seed.Application.Admin.Plans.Commands.UpdatePlan;
using Seed.Application.Common;
using Seed.Application.Common.Interfaces;
using Seed.Application.Common.Models;
using Seed.Domain.Authorization;
using Seed.Domain.Entities;
using Seed.Infrastructure.Persistence;

namespace Seed.Infrastructure.Billing.Commands;

public sealed class UpdatePlanCommandHandler(
    ApplicationDbContext dbContext,
    IPaymentGateway paymentGateway,
    IAuditService auditService)
    : IRequestHandler<UpdatePlanCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(
        UpdatePlanCommand request, CancellationToken cancellationToken)
    {
        var plan = await dbContext.SubscriptionPlans
            .Include(p => p.Features)
            .FirstOrDefaultAsync(p => p.Id == request.PlanId, cancellationToken);

        if (plan is null)
            return Result<bool>.Failure("Plan not found.");

        plan.Name = request.Name;
        plan.Description = request.Description;
        plan.MonthlyPrice = request.MonthlyPrice;
        plan.YearlyPrice = request.YearlyPrice;
        plan.TrialDays = request.TrialDays;
        plan.IsFreeTier = request.IsFreeTier;
        plan.IsDefault = request.IsDefault;
        plan.IsPopular = request.IsPopular;
        plan.SortOrder = request.SortOrder;
        plan.UpdatedAt = DateTime.UtcNow;

        // Remove features not in the request
        var requestFeatureKeys = request.Features.Select(f => f.Key).ToHashSet();
        var featuresToRemove = plan.Features.Where(f => !requestFeatureKeys.Contains(f.Key)).ToList();
        foreach (var feature in featuresToRemove)
            dbContext.Set<PlanFeature>().Remove(feature);

        // Update existing and add new features
        foreach (var f in request.Features)
        {
            var existing = plan.Features.FirstOrDefault(ef => ef.Key == f.Key);
            if (existing is not null)
            {
                existing.Description = f.Description;
                existing.LimitValue = f.LimitValue;
                existing.SortOrder = f.SortOrder;
            }
            else
            {
                dbContext.Set<PlanFeature>().Add(new PlanFeature
                {
                    Id = Guid.NewGuid(),
                    PlanId = plan.Id,
                    Key = f.Key,
                    Description = f.Description,
                    LimitValue = f.LimitValue,
                    SortOrder = f.SortOrder
                });
            }
        }

        var syncResult = await paymentGateway.SyncPlanToProviderAsync(new SyncPlanRequest(
            ProductId: plan.StripeProductId,
            Name: plan.Name,
            Description: plan.Description,
            MonthlyPriceInCents: (long)(plan.MonthlyPrice * 100),
            YearlyPriceInCents: (long)(plan.YearlyPrice * 100),
            ExistingMonthlyPriceId: plan.StripePriceIdMonthly,
            ExistingYearlyPriceId: plan.StripePriceIdYearly), cancellationToken);

        plan.StripeProductId = syncResult.ProductId;
        plan.StripePriceIdMonthly = syncResult.MonthlyPriceId;
        plan.StripePriceIdYearly = syncResult.YearlyPriceId;

        await dbContext.SaveChangesAsync(cancellationToken);

        await auditService.LogAsync(
            AuditActions.PlanUpdated,
            entityType: "SubscriptionPlan",
            entityId: plan.Id.ToString(),
            details: $"Plan '{plan.Name}' updated",
            userId: request.CurrentUserId,
            ipAddress: request.IpAddress,
            userAgent: request.UserAgent,
            cancellationToken: cancellationToken);

        return Result<bool>.Success(true);
    }
}
