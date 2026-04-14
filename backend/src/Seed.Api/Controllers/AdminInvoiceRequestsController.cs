using System.Security.Claims;
using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Seed.Api.Authorization;
using Seed.Application.Admin.InvoiceRequests.Commands.UpdateInvoiceRequestStatus;
using Seed.Application.Admin.InvoiceRequests.Queries.GetInvoiceRequests;
using Seed.Domain.Authorization;

namespace Seed.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/invoice-requests")]
[Authorize]
public class AdminInvoiceRequestsController(ISender sender) : ControllerBase
{
    private Guid CurrentUserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private string? IpAddress => HttpContext.Connection.RemoteIpAddress?.ToString();
    private string? UserAgent => Request.Headers.UserAgent.ToString();

    [HttpGet]
    [HasPermission(Permissions.Subscriptions.Read)]
    public async Task<IActionResult> GetInvoiceRequests(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? status = null)
    {
        var query = new GetInvoiceRequestsQuery
        {
            PageNumber = pageNumber,
            PageSize = pageSize,
            StatusFilter = status
        };
        var result = await sender.Send(query);
        return result.Succeeded ? Ok(result.Data) : BadRequest(new { errors = result.Errors });
    }

    [HttpPut("{id:guid}/status")]
    [HasPermission(Permissions.Subscriptions.Manage)]
    public async Task<IActionResult> UpdateStatus(Guid id, UpdateInvoiceRequestStatusCommand command)
    {
        var enrichedCommand = command with
        {
            InvoiceRequestId = id,
            CurrentUserId = CurrentUserId,
            IpAddress = IpAddress,
            UserAgent = UserAgent
        };

        var result = await sender.Send(enrichedCommand);
        return result.Succeeded ? Ok() : BadRequest(new { errors = result.Errors });
    }
}
