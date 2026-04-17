using MediatR;
using Microsoft.EntityFrameworkCore;
using Seed.Application.Billing.Models;
using Seed.Application.Billing.Queries.GetMySubscription;
using Seed.Application.Common;
using Seed.Domain.Enums;
using Seed.Infrastructure.Persistence;

namespace Seed.Infrastructure.Billing.Queries;

public sealed class GetMySubscriptionQueryHandler(ApplicationDbContext dbContext)
    : IRequestHandler<GetMySubscriptionQuery, Result<UserSubscriptionDto?>>
{
    public async Task<Result<UserSubscriptionDto?>> Handle(
        GetMySubscriptionQuery request, CancellationToken cancellationToken)
    {
        var subscription = await dbContext.UserSubscriptions
            .AsNoTracking()
            .Where(s => s.UserId == request.UserId
                && (s.Status == SubscriptionStatus.Active || s.Status == SubscriptionStatus.Trialing))
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => new UserSubscriptionDto(
                s.Id, s.Plan.Name, s.Plan.Description,
                s.Status.ToString(), s.Plan.MonthlyPrice, s.Plan.YearlyPrice,
                s.CurrentPeriodStart, s.CurrentPeriodEnd,
                s.TrialEnd, s.CanceledAt,
                s.Plan.IsFreeTier,
                s.Plan.Features
                    .OrderBy(f => f.SortOrder)
                    .Select(f => new PlanFeatureDto(
                        f.Id, f.Key, f.Description, f.LimitValue, f.SortOrder))
                    .ToList(),
                s.ScheduledPlan != null ? s.ScheduledPlan.Name : null,
                s.ScheduledPlanId != null ? s.CurrentPeriodEnd : null))
            .FirstOrDefaultAsync(cancellationToken);

        return Result<UserSubscriptionDto?>.Success(subscription);
    }
}
