using System.Security.Claims;
using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Seed.Api.Authorization;
using Seed.Application.Admin.Plans.Commands.ArchivePlan;
using Seed.Application.Admin.Plans.Commands.CreatePlan;
using Seed.Application.Admin.Plans.Commands.UpdatePlan;
using Seed.Application.Admin.Plans.Queries.GetAdminPlanById;
using Seed.Application.Admin.Plans.Queries.GetAdminPlans;
using Seed.Domain.Authorization;

namespace Seed.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/plans")]
[Authorize]
public class AdminPlansController(ISender sender) : ControllerBase
{
    private Guid CurrentUserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private string? IpAddress => HttpContext.Connection.RemoteIpAddress?.ToString();
    private string? UserAgent => Request.Headers.UserAgent.ToString();

    [HttpGet]
    [HasPermission(Permissions.Plans.Read)]
    public async Task<IActionResult> GetPlans()
    {
        var result = await sender.Send(new GetAdminPlansQuery());
        return result.Succeeded ? Ok(result.Data) : BadRequest(new { errors = result.Errors });
    }

    [HttpGet("{id:guid}")]
    [HasPermission(Permissions.Plans.Read)]
    public async Task<IActionResult> GetPlanById(Guid id)
    {
        var result = await sender.Send(new GetAdminPlanByIdQuery(id));
        return result.Succeeded ? Ok(result.Data) : NotFound(new { errors = result.Errors });
    }

    [HttpPost]
    [HasPermission(Permissions.Plans.Create)]
    public async Task<IActionResult> CreatePlan(CreatePlanCommand command)
    {
        var enrichedCommand = command with
        {
            CurrentUserId = CurrentUserId,
            IpAddress = IpAddress,
            UserAgent = UserAgent
        };
        var result = await sender.Send(enrichedCommand);
        return result.Succeeded
            ? CreatedAtAction(nameof(GetPlanById), new { id = result.Data }, new { id = result.Data })
            : BadRequest(new { errors = result.Errors });
    }

    [HttpPut("{id:guid}")]
    [HasPermission(Permissions.Plans.Update)]
    public async Task<IActionResult> UpdatePlan(Guid id, UpdatePlanCommand command)
    {
        var enrichedCommand = command with
        {
            PlanId = id,
            CurrentUserId = CurrentUserId,
            IpAddress = IpAddress,
            UserAgent = UserAgent
        };
        var result = await sender.Send(enrichedCommand);
        return result.Succeeded ? NoContent() : BadRequest(new { errors = result.Errors });
    }

    [HttpPost("{id:guid}/archive")]
    [HasPermission(Permissions.Plans.Update)]
    public async Task<IActionResult> ArchivePlan(Guid id)
    {
        var command = new ArchivePlanCommand(id)
        {
            CurrentUserId = CurrentUserId,
            IpAddress = IpAddress,
            UserAgent = UserAgent
        };
        var result = await sender.Send(command);
        return result.Succeeded ? NoContent() : BadRequest(new { errors = result.Errors });
    }
}
