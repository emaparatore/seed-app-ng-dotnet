using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Seed.Application.Admin.SystemHealth.Queries.GetSystemHealth;
using Seed.Api.Authorization;
using Seed.Domain.Authorization;

namespace Seed.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/system-health")]
[Authorize]
public class AdminSystemHealthController(ISender sender) : ControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.SystemHealth.Read)]
    public async Task<IActionResult> GetSystemHealth()
    {
        var result = await sender.Send(new GetSystemHealthQuery());
        return result.Succeeded ? Ok(result.Data) : BadRequest(new { errors = result.Errors });
    }
}
