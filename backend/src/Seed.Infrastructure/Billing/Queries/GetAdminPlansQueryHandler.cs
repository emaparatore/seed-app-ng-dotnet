using MediatR;
using Microsoft.EntityFrameworkCore;
using Seed.Application.Admin.Plans.Models;
using Seed.Application.Admin.Plans.Queries.GetAdminPlans;
using Seed.Application.Billing.Models;
using Seed.Application.Common;
using Seed.Domain.Enums;
using Seed.Infrastructure.Persistence;

namespace Seed.Infrastructure.Billing.Queries;

public sealed class GetAdminPlansQueryHandler(ApplicationDbContext dbContext)
    : IRequestHandler<GetAdminPlansQuery, Result<IReadOnlyList<AdminPlanDto>>>
{
    public async Task<Result<IReadOnlyList<AdminPlanDto>>> Handle(
        GetAdminPlansQuery request, CancellationToken cancellationToken)
    {
        var plans = await dbContext.SubscriptionPlans
            .AsNoTracking()
            .OrderBy(p => p.SortOrder)
            .Select(p => new AdminPlanDto(
                p.Id, p.Name, p.Description,
                p.MonthlyPrice, p.YearlyPrice,
                p.StripePriceIdMonthly, p.StripePriceIdYearly, p.StripeProductId,
                p.TrialDays, p.IsFreeTier, p.IsDefault, p.IsPopular,
                p.Status.ToString(), p.SortOrder,
                p.CreatedAt, p.UpdatedAt,
                p.Subscriptions.Count(s => s.Status == SubscriptionStatus.Active || s.Status == SubscriptionStatus.Trialing),
                p.Features
                    .OrderBy(f => f.SortOrder)
                    .Select(f => new PlanFeatureDto(f.Id, f.Key, f.Description, f.LimitValue, f.SortOrder))
                    .ToList()))
            .ToListAsync(cancellationToken);

        return Result<IReadOnlyList<AdminPlanDto>>.Success(plans);
    }
}
