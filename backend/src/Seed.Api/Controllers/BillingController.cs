using System.Security.Claims;
using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Seed.Application.Billing.Commands.CancelSubscription;
using Seed.Application.Billing.Commands.ChangePlan;
using Seed.Application.Billing.Commands.CreateCheckoutSession;
using Seed.Application.Billing.Commands.CreateInvoiceRequest;
using Seed.Application.Billing.Commands.CreatePortalSession;
using Seed.Application.Billing.Queries.GetMyInvoiceRequests;
using Seed.Application.Billing.Queries.GetMySubscription;

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

    [HttpGet("subscription")]
    public async Task<IActionResult> GetMySubscription()
    {
        var result = await sender.Send(new GetMySubscriptionQuery(CurrentUserId));
        return result.Succeeded ? new JsonResult(result.Data) : BadRequest(new { errors = result.Errors });
    }

    [HttpPost("portal")]
    public async Task<IActionResult> CreatePortalSession(CreatePortalSessionCommand command)
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

    [HttpPost("change-plan")]
    public async Task<IActionResult> ChangePlan(ChangePlanCommand command)
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

    [HttpPost("cancel")]
    public async Task<IActionResult> CancelSubscription()
    {
        var command = new CancelSubscriptionCommand
        {
            UserId = CurrentUserId,
            IpAddress = IpAddress,
            UserAgent = UserAgent
        };

        var result = await sender.Send(command);
        return result.Succeeded ? Ok() : BadRequest(new { errors = result.Errors });
    }

    [HttpPost("invoice-request")]
    public async Task<IActionResult> CreateInvoiceRequest(CreateInvoiceRequestCommand command)
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

    [HttpGet("invoice-requests")]
    public async Task<IActionResult> GetMyInvoiceRequests()
    {
        var result = await sender.Send(new GetMyInvoiceRequestsQuery(CurrentUserId));
        return result.Succeeded ? Ok(result.Data) : BadRequest(new { errors = result.Errors });
    }
}
