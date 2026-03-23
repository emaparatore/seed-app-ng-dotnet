using MediatR;
using Seed.Application.Admin.Dashboard.Models;
using Seed.Application.Common;

namespace Seed.Application.Admin.Dashboard.Queries.GetDashboardStats;

public sealed record GetDashboardStatsQuery : IRequest<Result<DashboardStatsDto>>;
