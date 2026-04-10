using System.Security.Claims;
using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Seed.Application.Billing.Commands.CreateCheckoutSession;

namespace Seed.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/billing")]
[Authorize]
public class BillingController(ISender sender) : ControllerBase
{
    private Guid CurrentUserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private string? IpAddress => HttpContext.Connection.RemoteIpAddress?.ToString();
    private string? UserAgent => Request.Headers.UserAgent.ToString();

    [HttpPost("checkout")]
    public async Task<IActionResult> CreateCheckoutSession(CreateCheckoutSessionCommand command)
    {
        var enrichedCommand = command with
        {
            UserId = CurrentUserId,
            IpAddress = IpAddress,
            UserAgent = UserAgent
        };

        var result = await sender.Send(enrichedCommand);
        return result.Succeeded ? Ok(result.Data) : BadRequest(new { errors = result.Errors });
    }
}
