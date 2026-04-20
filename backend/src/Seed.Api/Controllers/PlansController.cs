using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Seed.Application.Billing.Queries.GetPlans;

namespace Seed.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/plans")]
[AllowAnonymous]
public class PlansController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetPlans(CancellationToken ct)
    {
        var result = await sender.Send(new GetPlansQuery(), ct);
        return result.Succeeded ? Ok(result.Data) : BadRequest(new { errors = result.Errors });
    }
}
