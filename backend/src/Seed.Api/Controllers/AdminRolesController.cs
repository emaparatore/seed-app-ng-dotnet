using System.Security.Claims;
using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Seed.Api.Authorization;
using Seed.Application.Admin.Roles.Commands.CreateRole;
using Seed.Application.Admin.Roles.Commands.DeleteRole;
using Seed.Application.Admin.Roles.Commands.UpdateRole;
using Seed.Application.Admin.Roles.Queries.GetPermissions;
using Seed.Application.Admin.Roles.Queries.GetRoleById;
using Seed.Application.Admin.Roles.Queries.GetRoles;
using Seed.Domain.Authorization;

namespace Seed.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/roles")]
[Authorize]
public class AdminRolesController(ISender sender) : ControllerBase
{
    private Guid CurrentUserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private string? IpAddress => HttpContext.Connection.RemoteIpAddress?.ToString();
    private string? UserAgent => Request.Headers.UserAgent.ToString();

    [HttpGet]
    [HasPermission(Permissions.Roles.Read)]
    public async Task<IActionResult> GetRoles()
    {
        var result = await sender.Send(new GetRolesQuery());
        return result.Succeeded ? Ok(result.Data) : BadRequest(new { errors = result.Errors });
    }

    [HttpGet("{id:guid}")]
    [HasPermission(Permissions.Roles.Read)]
    public async Task<IActionResult> GetRoleById(Guid id)
    {
        var result = await sender.Send(new GetRoleByIdQuery(id));
        return result.Succeeded ? Ok(result.Data) : NotFound(new { errors = result.Errors });
    }

    [HttpPost]
    [HasPermission(Permissions.Roles.Create)]
    public async Task<IActionResult> CreateRole(CreateRoleCommand command)
    {
        var enrichedCommand = command with
        {
            CurrentUserId = CurrentUserId,
            IpAddress = IpAddress,
            UserAgent = UserAgent
        };
        var result = await sender.Send(enrichedCommand);
        return result.Succeeded
            ? CreatedAtAction(nameof(GetRoleById), new { id = result.Data }, new { id = result.Data })
            : BadRequest(new { errors = result.Errors });
    }

    [HttpPut("{id:guid}")]
    [HasPermission(Permissions.Roles.Update)]
    public async Task<IActionResult> UpdateRole(Guid id, UpdateRoleCommand command)
    {
        var enrichedCommand = command with
        {
            RoleId = id,
            CurrentUserId = CurrentUserId,
            IpAddress = IpAddress,
            UserAgent = UserAgent
        };
        var result = await sender.Send(enrichedCommand);
        return result.Succeeded ? NoContent() : BadRequest(new { errors = result.Errors });
    }

    [HttpDelete("{id:guid}")]
    [HasPermission(Permissions.Roles.Delete)]
    public async Task<IActionResult> DeleteRole(Guid id)
    {
        var command = new DeleteRoleCommand(id)
        {
            CurrentUserId = CurrentUserId,
            IpAddress = IpAddress,
            UserAgent = UserAgent
        };
        var result = await sender.Send(command);
        return result.Succeeded ? NoContent() : BadRequest(new { errors = result.Errors });
    }

    [HttpGet("permissions")]
    [HasPermission(Permissions.Roles.Read)]
    public async Task<IActionResult> GetPermissions()
    {
        var result = await sender.Send(new GetPermissionsQuery());
        return result.Succeeded ? Ok(result.Data) : BadRequest(new { errors = result.Errors });
    }
}
