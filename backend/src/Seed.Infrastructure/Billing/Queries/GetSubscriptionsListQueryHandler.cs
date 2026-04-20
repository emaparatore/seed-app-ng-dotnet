using MediatR;
using Microsoft.EntityFrameworkCore;
using Seed.Application.Admin.Subscriptions.Models;
using Seed.Application.Admin.Subscriptions.Queries.GetSubscriptionsList;
using Seed.Application.Common;
using Seed.Application.Common.Models;
using Seed.Domain.Enums;
using Seed.Infrastructure.Persistence;

namespace Seed.Infrastructure.Billing.Queries;

public sealed class GetSubscriptionsListQueryHandler(ApplicationDbContext dbContext)
    : IRequestHandler<GetSubscriptionsListQuery, Result<PagedResult<AdminSubscriptionDto>>>
{
    public async Task<Result<PagedResult<AdminSubscriptionDto>>> Handle(
        GetSubscriptionsListQuery request, CancellationToken cancellationToken)
    {
        var query = dbContext.UserSubscriptions
            .AsNoTracking()
            .Include(s => s.Plan)
            .Include(s => s.User)
            .AsQueryable();

        if (request.PlanIdFilter.HasValue)
            query = query.Where(s => s.PlanId == request.PlanIdFilter.Value);

        if (!string.IsNullOrWhiteSpace(request.StatusFilter) &&
            Enum.TryParse<SubscriptionStatus>(request.StatusFilter, ignoreCase: true, out var status))
        {
            query = query.Where(s => s.Status == status);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(s => s.CreatedAt)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(s => new AdminSubscriptionDto(
                s.Id,
                s.User != null ? s.User.Email! : "[anonymized]",
                s.Plan.Name,
                s.Status.ToString(),
                s.CurrentPeriodStart,
                s.CurrentPeriodEnd,
                s.TrialEnd,
                s.CanceledAt,
                s.CreatedAt))
            .ToListAsync(cancellationToken);

        return Result<PagedResult<AdminSubscriptionDto>>.Success(
            new PagedResult<AdminSubscriptionDto>(items, request.PageNumber, request.PageSize, totalCount));
    }
}
