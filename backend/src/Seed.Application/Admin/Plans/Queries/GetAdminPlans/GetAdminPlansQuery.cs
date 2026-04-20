using MediatR;
using Seed.Application.Admin.Plans.Models;
using Seed.Application.Common;

namespace Seed.Application.Admin.Plans.Queries.GetAdminPlans;

public sealed record GetAdminPlansQuery : IRequest<Result<IReadOnlyList<AdminPlanDto>>>;
