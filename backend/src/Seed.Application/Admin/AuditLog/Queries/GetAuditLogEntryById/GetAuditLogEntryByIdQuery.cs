using MediatR;
using Seed.Application.Admin.AuditLog.Models;
using Seed.Application.Common;

namespace Seed.Application.Admin.AuditLog.Queries.GetAuditLogEntryById;

public sealed record GetAuditLogEntryByIdQuery(Guid Id) : IRequest<Result<AuditLogEntryDto>>;
