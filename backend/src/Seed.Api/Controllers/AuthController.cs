using System.Security.Claims;
using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Seed.Application.Auth.Commands.Login;
using Seed.Application.Auth.Commands.Logout;
using Seed.Application.Auth.Commands.RefreshToken;
using Seed.Application.Auth.Commands.Register;
using Seed.Application.Auth.Queries.GetCurrentUser;

namespace Seed.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/auth")]
public class AuthController(ISender sender) : ControllerBase
{
    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterCommand command)
    {
        var result = await sender.Send(command);
        return result.Succeeded ? Ok(result.Data) : BadRequest(new { errors = result.Errors });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginCommand command)
    {
        var result = await sender.Send(command);
        return result.Succeeded ? Ok(result.Data) : Unauthorized(new { errors = result.Errors });
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(RefreshTokenCommand command)
    {
        var result = await sender.Send(command);
        return result.Succeeded ? Ok(result.Data) : Unauthorized(new { errors = result.Errors });
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(LogoutCommand command)
    {
        var result = await sender.Send(command);
        return result.Succeeded ? NoContent() : BadRequest(new { errors = result.Errors });
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var result = await sender.Send(new GetCurrentUserQuery(userId));
        return result.Succeeded ? Ok(result.Data) : NotFound(new { errors = result.Errors });
    }
}
