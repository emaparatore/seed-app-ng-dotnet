using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Seed.Application.Admin.Dashboard.Queries.GetDashboardStats;
using Seed.Api.Authorization;
using Seed.Domain.Authorization;

namespace Seed.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/dashboard")]
[Authorize]
public class AdminDashboardController(ISender sender) : ControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.Dashboard.ViewStats)]
    public async Task<IActionResult> GetDashboardStats()
    {
        var result = await sender.Send(new GetDashboardStatsQuery());
        return result.Succeeded ? Ok(result.Data) : BadRequest(new { errors = result.Errors });
    }
}
