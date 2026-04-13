using MediatR;
using Seed.Application.Admin.Subscriptions.Models;
using Seed.Application.Common;
using Seed.Application.Common.Models;

namespace Seed.Application.Admin.Subscriptions.Queries.GetSubscriptionsList;

public sealed record GetSubscriptionsListQuery : IRequest<Result<PagedResult<AdminSubscriptionDto>>>
{
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 10;
    public Guid? PlanIdFilter { get; init; }
    public string? StatusFilter { get; init; }
}
