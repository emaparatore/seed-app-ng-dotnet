using MediatR;
using Seed.Application.Admin.Subscriptions.Models;
using Seed.Application.Common;

namespace Seed.Application.Admin.Subscriptions.Queries.GetSubscriptionDetail;

public sealed record GetSubscriptionDetailQuery(Guid Id) : IRequest<Result<AdminSubscriptionDetailDto>>;
