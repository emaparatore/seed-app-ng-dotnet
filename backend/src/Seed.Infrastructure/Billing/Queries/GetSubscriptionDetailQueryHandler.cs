using MediatR;
using Microsoft.EntityFrameworkCore;
using Seed.Application.Admin.Subscriptions.Models;
using Seed.Application.Admin.Subscriptions.Queries.GetSubscriptionDetail;
using Seed.Application.Common;
using Seed.Infrastructure.Persistence;

namespace Seed.Infrastructure.Billing.Queries;

public sealed class GetSubscriptionDetailQueryHandler(ApplicationDbContext dbContext)
    : IRequestHandler<GetSubscriptionDetailQuery, Result<AdminSubscriptionDetailDto>>
{
    public async Task<Result<AdminSubscriptionDetailDto>> Handle(
        GetSubscriptionDetailQuery request, CancellationToken cancellationToken)
    {
        var subscription = await dbContext.UserSubscriptions
            .AsNoTracking()
            .Include(s => s.Plan)
            .Include(s => s.User)
            .Where(s => s.Id == request.Id)
            .Select(s => new AdminSubscriptionDetailDto(
                s.Id,
                s.UserId,
                s.User.Email!,
                s.User.FirstName + " " + s.User.LastName,
                s.PlanId,
                s.Plan.Name,
                s.Plan.MonthlyPrice,
                s.Plan.YearlyPrice,
                s.Status.ToString(),
                s.StripeSubscriptionId,
                s.StripeCustomerId,
                s.CurrentPeriodStart,
                s.CurrentPeriodEnd,
                s.TrialEnd,
                s.CanceledAt,
                s.CreatedAt,
                s.UpdatedAt))
            .FirstOrDefaultAsync(cancellationToken);

        if (subscription is null)
            return Result<AdminSubscriptionDetailDto>.Failure("Subscription not found");

        return Result<AdminSubscriptionDetailDto>.Success(subscription);
    }
}
