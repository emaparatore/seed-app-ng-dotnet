using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Seed.Api.Authorization;
using Seed.Application.Admin.Subscriptions.Queries.GetSubscriptionDetail;
using Seed.Application.Admin.Subscriptions.Queries.GetSubscriptionMetrics;
using Seed.Application.Admin.Subscriptions.Queries.GetSubscriptionsList;
using Seed.Domain.Authorization;

namespace Seed.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/subscriptions")]
[Authorize]
public class AdminSubscriptionsController(ISender sender) : ControllerBase
{
    [HttpGet("metrics")]
    [HasPermission(Permissions.Subscriptions.Read)]
    public async Task<IActionResult> GetMetrics()
    {
        var result = await sender.Send(new GetSubscriptionMetricsQuery());
        return result.Succeeded ? Ok(result.Data) : BadRequest(new { errors = result.Errors });
    }

    [HttpGet]
    [HasPermission(Permissions.Subscriptions.Read)]
    public async Task<IActionResult> GetSubscriptions(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] Guid? planId = null,
        [FromQuery] string? status = null)
    {
        var query = new GetSubscriptionsListQuery
        {
            PageNumber = pageNumber,
            PageSize = pageSize,
            PlanIdFilter = planId,
            StatusFilter = status
        };
        var result = await sender.Send(query);
        return result.Succeeded ? Ok(result.Data) : BadRequest(new { errors = result.Errors });
    }

    [HttpGet("{id:guid}")]
    [HasPermission(Permissions.Subscriptions.Read)]
    public async Task<IActionResult> GetSubscriptionById(Guid id)
    {
        var result = await sender.Send(new GetSubscriptionDetailQuery(id));
        return result.Succeeded ? Ok(result.Data) : NotFound(new { errors = result.Errors });
    }
}
