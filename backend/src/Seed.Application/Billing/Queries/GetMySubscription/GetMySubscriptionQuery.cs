using MediatR;
using Seed.Application.Billing.Models;
using Seed.Application.Common;

namespace Seed.Application.Billing.Queries.GetMySubscription;

public sealed record GetMySubscriptionQuery(Guid UserId) : IRequest<Result<UserSubscriptionDto?>>;
