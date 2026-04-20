using MediatR;
using Seed.Application.Billing.Models;
using Seed.Application.Common;

namespace Seed.Application.Billing.Queries.GetPlans;

public sealed record GetPlansQuery : IRequest<Result<IReadOnlyList<PlanDto>>>;
