using System.Security.Claims;
using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Seed.Api.Authorization;
using Seed.Application.Admin.Settings.Commands.UpdateSystemSettings;
using Seed.Application.Admin.Settings.Queries.GetSystemSettings;
using Seed.Domain.Authorization;

namespace Seed.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/settings")]
[Authorize]
public class AdminSettingsController(ISender sender) : ControllerBase
{
    private Guid CurrentUserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private string? IpAddress => HttpContext.Connection.RemoteIpAddress?.ToString();
    private string? UserAgent => Request.Headers.UserAgent.ToString();

    [HttpGet]
    [HasPermission(Permissions.Settings.Read)]
    public async Task<IActionResult> GetSettings()
    {
        var result = await sender.Send(new GetSystemSettingsQuery());
        return result.Succeeded ? Ok(result.Data) : BadRequest(new { errors = result.Errors });
    }

    [HttpPut]
    [HasPermission(Permissions.Settings.Manage)]
    public async Task<IActionResult> UpdateSettings(UpdateSystemSettingsCommand command)
    {
        var enrichedCommand = command with
        {
            CurrentUserId = CurrentUserId,
            IpAddress = IpAddress,
            UserAgent = UserAgent
        };
        var result = await sender.Send(enrichedCommand);
        return result.Succeeded ? NoContent() : BadRequest(new { errors = result.Errors });
    }
}
