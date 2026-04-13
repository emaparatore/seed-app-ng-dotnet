using MediatR;
using Seed.Application.Admin.Subscriptions.Models;
using Seed.Application.Common;

namespace Seed.Application.Admin.Subscriptions.Queries.GetSubscriptionMetrics;

public sealed record GetSubscriptionMetricsQuery : IRequest<Result<SubscriptionMetricsDto>>;
