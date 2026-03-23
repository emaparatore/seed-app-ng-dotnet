using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Seed.Api.Authorization;
using Seed.Application.Admin.AuditLog.Queries.ExportAuditLog;
using Seed.Application.Admin.AuditLog.Queries.GetAuditLogEntries;
using Seed.Application.Admin.AuditLog.Queries.GetAuditLogEntryById;
using Seed.Domain.Authorization;

namespace Seed.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/audit-log")]
[Authorize]
public class AdminAuditLogController(ISender sender) : ControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.AuditLog.Read)]
    public async Task<IActionResult> GetAuditLogEntries([FromQuery] GetAuditLogEntriesQuery query)
    {
        var result = await sender.Send(query);
        return result.Succeeded ? Ok(result.Data) : BadRequest(new { errors = result.Errors });
    }

    [HttpGet("{id:guid}")]
    [HasPermission(Permissions.AuditLog.Read)]
    public async Task<IActionResult> GetAuditLogEntryById(Guid id)
    {
        var result = await sender.Send(new GetAuditLogEntryByIdQuery(id));
        return result.Succeeded ? Ok(result.Data) : NotFound(new { errors = result.Errors });
    }

    [HttpGet("export")]
    [HasPermission(Permissions.AuditLog.Export)]
    public async Task<IActionResult> ExportAuditLog([FromQuery] ExportAuditLogQuery query)
    {
        var result = await sender.Send(query);
        return result.Succeeded
            ? File(result.Data!, "text/csv", "audit-log.csv")
            : BadRequest(new { errors = result.Errors });
    }
}
