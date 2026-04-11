using MediatR;
using Seed.Application.Admin.Plans.Models;
using Seed.Application.Common;

namespace Seed.Application.Admin.Plans.Queries.GetAdminPlanById;

public sealed record GetAdminPlanByIdQuery(Guid PlanId) : IRequest<Result<AdminPlanDto>>;
