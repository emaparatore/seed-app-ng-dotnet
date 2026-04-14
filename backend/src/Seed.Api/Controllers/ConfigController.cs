using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Seed.Shared.Extensions;

namespace Seed.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/config")]
[AllowAnonymous]
public class ConfigController(IConfiguration configuration) : ControllerBase
{
    [HttpGet]
    public IActionResult GetConfig()
    {
        return Ok(new { paymentsEnabled = configuration.IsPaymentsModuleEnabled() });
    }
}
