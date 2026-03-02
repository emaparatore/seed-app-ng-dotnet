using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;

namespace Seed.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
public class HomeController : ControllerBase
{
    [HttpGet(Name = "GetGreeting")]
    public IActionResult Get()
    {
        return Ok(new { message = "Hello, Seed!" });
    }
}
