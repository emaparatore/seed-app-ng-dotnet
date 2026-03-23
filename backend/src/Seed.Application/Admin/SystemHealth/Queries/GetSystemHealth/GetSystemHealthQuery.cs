using MediatR;
using Seed.Application.Admin.SystemHealth.Models;
using Seed.Application.Common;

namespace Seed.Application.Admin.SystemHealth.Queries.GetSystemHealth;

public sealed record GetSystemHealthQuery : IRequest<Result<SystemHealthDto>>;
