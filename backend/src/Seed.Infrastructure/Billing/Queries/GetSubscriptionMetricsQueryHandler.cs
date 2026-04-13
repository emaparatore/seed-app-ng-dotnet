using MediatR;
using Microsoft.EntityFrameworkCore;
using Seed.Application.Admin.Subscriptions.Models;
using Seed.Application.Admin.Subscriptions.Queries.GetSubscriptionMetrics;
using Seed.Application.Common;
using Seed.Domain.Enums;
using Seed.Infrastructure.Persistence;

namespace Seed.Infrastructure.Billing.Queries;

public sealed class GetSubscriptionMetricsQueryHandler(ApplicationDbContext dbContext)
    : IRequestHandler<GetSubscriptionMetricsQuery, Result<SubscriptionMetricsDto>>
{
    public async Task<Result<SubscriptionMetricsDto>> Handle(
        GetSubscriptionMetricsQuery request, CancellationToken cancellationToken)
    {
        var subscriptions = await dbContext.UserSubscriptions
            .AsNoTracking()
            .Include(s => s.Plan)
            .ToListAsync(cancellationToken);

        var activeCount = subscriptions.Count(s => s.Status == SubscriptionStatus.Active);
        var trialingCount = subscriptions.Count(s => s.Status == SubscriptionStatus.Trialing);

        var mrr = subscriptions
            .Where(s => s.Status == SubscriptionStatus.Active || s.Status == SubscriptionStatus.Trialing)
            .Sum(s =>
            {
                var periodDays = (s.CurrentPeriodEnd - s.CurrentPeriodStart).TotalDays;
                return periodDays > 35 ? s.Plan.YearlyPrice / 12 : s.Plan.MonthlyPrice;
            });

        var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);
        var canceledLast30Days = subscriptions.Count(s =>
            s.Status == SubscriptionStatus.Canceled && s.CanceledAt >= thirtyDaysAgo);

        var churnDenominator = activeCount + trialingCount + canceledLast30Days;
        var churnRate = churnDenominator > 0
            ? Math.Round((decimal)canceledLast30Days / churnDenominator, 4)
            : 0m;

        return Result<SubscriptionMetricsDto>.Success(
            new SubscriptionMetricsDto(mrr, activeCount, trialingCount, churnRate));
    }
}
