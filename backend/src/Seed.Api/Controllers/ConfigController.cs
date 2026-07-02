using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Seed.Application.Common.Interfaces;
using Seed.Shared.Extensions;

namespace Seed.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/config")]
[AllowAnonymous]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class ConfigController(
    IConfiguration configuration,
    ISystemSettingsService systemSettingsService) : ControllerBase
{
    private const string DefaultAppName = "Seed App";

    [HttpGet]
    public async Task<IActionResult> GetConfig(CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
        Response.Headers.Pragma = "no-cache";

        var appName = await systemSettingsService.GetValueAsync("General.AppName", cancellationToken)
            ?? DefaultAppName;

        return Ok(new
        {
            paymentsEnabled = configuration.IsPaymentsModuleEnabled(),
            appName
        });
    }
}
