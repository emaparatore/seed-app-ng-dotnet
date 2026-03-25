using System.Security.Claims;
using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Seed.Api.Authorization;
using Seed.Application.Admin.Users.Commands.AdminResetPassword;
using Seed.Application.Admin.Users.Commands.AssignUserRoles;
using Seed.Application.Admin.Users.Commands.CreateUser;
using Seed.Application.Admin.Users.Commands.DeleteUser;
using Seed.Application.Admin.Users.Commands.ForcePasswordChange;
using Seed.Application.Admin.Users.Commands.ToggleUserStatus;
using Seed.Application.Admin.Users.Commands.UpdateUser;
using Seed.Application.Admin.Users.Queries.GetUserById;
using Seed.Application.Admin.Users.Queries.GetUsers;
using Seed.Domain.Authorization;

namespace Seed.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/users")]
[Authorize]
public class AdminUsersController(ISender sender) : ControllerBase
{
    private Guid CurrentUserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private string? IpAddress => HttpContext.Connection.RemoteIpAddress?.ToString();
    private string? UserAgent => Request.Headers.UserAgent.ToString();

    [HttpGet]
    [HasPermission(Permissions.Users.Read)]
    public async Task<IActionResult> GetUsers([FromQuery] GetUsersQuery query)
    {
        var result = await sender.Send(query);
        return result.Succeeded ? Ok(result.Data) : BadRequest(new { errors = result.Errors });
    }

    [HttpGet("{id:guid}")]
    [HasPermission(Permissions.Users.Read)]
    public async Task<IActionResult> GetUserById(Guid id)
    {
        var result = await sender.Send(new GetUserByIdQuery(id));
        return result.Succeeded ? Ok(result.Data) : NotFound(new { errors = result.Errors });
    }

    [HttpPost]
    [HasPermission(Permissions.Users.Create)]
    public async Task<IActionResult> CreateUser(CreateUserCommand command)
    {
        var enrichedCommand = command with
        {
            CurrentUserId = CurrentUserId,
            IpAddress = IpAddress,
            UserAgent = UserAgent
        };
        var result = await sender.Send(enrichedCommand);
        return result.Succeeded
            ? CreatedAtAction(nameof(GetUserById), new { id = result.Data }, new { id = result.Data })
            : BadRequest(new { errors = result.Errors });
    }

    [HttpPut("{id:guid}")]
    [HasPermission(Permissions.Users.Update)]
    public async Task<IActionResult> UpdateUser(Guid id, UpdateUserCommand command)
    {
        var enrichedCommand = command with
        {
            UserId = id,
            CurrentUserId = CurrentUserId,
            IpAddress = IpAddress,
            UserAgent = UserAgent
        };
        var result = await sender.Send(enrichedCommand);
        return result.Succeeded ? NoContent() : BadRequest(new { errors = result.Errors });
    }

    [HttpDelete("{id:guid}")]
    [HasPermission(Permissions.Users.Delete)]
    public async Task<IActionResult> DeleteUser(Guid id)
    {
        var command = new DeleteUserCommand(id)
        {
            CurrentUserId = CurrentUserId,
            IpAddress = IpAddress,
            UserAgent = UserAgent
        };
        var result = await sender.Send(command);
        return result.Succeeded ? NoContent() : BadRequest(new { errors = result.Errors });
    }

    [HttpPut("{id:guid}/status")]
    [HasPermission(Permissions.Users.ToggleStatus)]
    public async Task<IActionResult> ToggleUserStatus(Guid id, ToggleUserStatusCommand command)
    {
        var enrichedCommand = command with
        {
            UserId = id,
            CurrentUserId = CurrentUserId,
            IpAddress = IpAddress,
            UserAgent = UserAgent
        };
        var result = await sender.Send(enrichedCommand);
        return result.Succeeded ? NoContent() : BadRequest(new { errors = result.Errors });
    }

    [HttpPut("{id:guid}/roles")]
    [HasPermission(Permissions.Users.AssignRoles)]
    public async Task<IActionResult> AssignUserRoles(Guid id, AssignUserRolesCommand command)
    {
        var enrichedCommand = command with
        {
            UserId = id,
            CurrentUserId = CurrentUserId,
            IpAddress = IpAddress,
            UserAgent = UserAgent
        };
        var result = await sender.Send(enrichedCommand);
        return result.Succeeded ? NoContent() : BadRequest(new { errors = result.Errors });
    }

    [HttpPost("{id:guid}/force-password-change")]
    [HasPermission(Permissions.Users.Update)]
    public async Task<IActionResult> ForcePasswordChange(Guid id)
    {
        var command = new ForcePasswordChangeCommand(id)
        {
            CurrentUserId = CurrentUserId,
            IpAddress = IpAddress,
            UserAgent = UserAgent
        };
        var result = await sender.Send(command);
        return result.Succeeded ? NoContent() : BadRequest(new { errors = result.Errors });
    }

    [HttpPost("{id:guid}/reset-password")]
    [HasPermission(Permissions.Users.Update)]
    public async Task<IActionResult> ResetPassword(Guid id)
    {
        var command = new AdminResetPasswordCommand(id)
        {
            CurrentUserId = CurrentUserId,
            IpAddress = IpAddress,
            UserAgent = UserAgent
        };
        var result = await sender.Send(command);
        return result.Succeeded ? NoContent() : BadRequest(new { errors = result.Errors });
    }
}
