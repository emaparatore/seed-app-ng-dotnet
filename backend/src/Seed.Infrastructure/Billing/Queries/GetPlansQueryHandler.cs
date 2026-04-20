using MediatR;
using Microsoft.EntityFrameworkCore;
using Seed.Application.Billing.Models;
using Seed.Application.Billing.Queries.GetPlans;
using Seed.Application.Common;
using Seed.Domain.Enums;
using Seed.Infrastructure.Persistence;

namespace Seed.Infrastructure.Billing.Queries;

public sealed class GetPlansQueryHandler(ApplicationDbContext dbContext)
    : IRequestHandler<GetPlansQuery, Result<IReadOnlyList<PlanDto>>>
{
    public async Task<Result<IReadOnlyList<PlanDto>>> Handle(
        GetPlansQuery request, CancellationToken cancellationToken)
    {
        var plans = await dbContext.SubscriptionPlans
            .AsNoTracking()
            .Where(p => p.Status == PlanStatus.Active)
            .OrderBy(p => p.SortOrder)
            .Select(p => new PlanDto(
                p.Id, p.Name, p.Description,
                p.MonthlyPrice, p.YearlyPrice,
                p.TrialDays, p.IsFreeTier, p.IsDefault, p.IsPopular,
                p.SortOrder,
                p.Features
                    .OrderBy(f => f.SortOrder)
                    .Select(f => new PlanFeatureDto(
                        f.Id, f.Key, f.Description, f.LimitValue, f.SortOrder))
                    .ToList()))
            .ToListAsync(cancellationToken);

        return Result<IReadOnlyList<PlanDto>>.Success(plans);
    }
}
