using MediatR;
using Microsoft.EntityFrameworkCore;
using Seed.Application.Admin.Plans.Models;
using Seed.Application.Admin.Plans.Queries.GetAdminPlanById;
using Seed.Application.Billing.Models;
using Seed.Application.Common;
using Seed.Domain.Enums;
using Seed.Infrastructure.Persistence;

namespace Seed.Infrastructure.Billing.Queries;

public sealed class GetAdminPlanByIdQueryHandler(ApplicationDbContext dbContext)
    : IRequestHandler<GetAdminPlanByIdQuery, Result<AdminPlanDto>>
{
    public async Task<Result<AdminPlanDto>> Handle(
        GetAdminPlanByIdQuery request, CancellationToken cancellationToken)
    {
        var plan = await dbContext.SubscriptionPlans
            .AsNoTracking()
            .Where(p => p.Id == request.PlanId)
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
            .FirstOrDefaultAsync(cancellationToken);

        return plan is null
            ? Result<AdminPlanDto>.Failure("Plan not found.")
            : Result<AdminPlanDto>.Success(plan);
    }
}
