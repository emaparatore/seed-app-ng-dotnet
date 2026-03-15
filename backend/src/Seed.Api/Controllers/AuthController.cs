using System.Security.Claims;
using System.Threading.RateLimiting;
using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Seed.Application.Auth.Commands.ConfirmEmail;
using Seed.Application.Auth.Commands.ForgotPassword;
using Seed.Application.Auth.Commands.Login;
using Seed.Application.Auth.Commands.Logout;
using Seed.Application.Auth.Commands.RefreshToken;
using Seed.Application.Auth.Commands.Register;
using Seed.Application.Auth.Commands.DeleteAccount;
using Seed.Application.Auth.Commands.ResetPassword;
using Seed.Application.Auth.Queries.GetCurrentUser;

namespace Seed.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/auth")]
public class AuthController(ISender sender) : ControllerBase
{
    [HttpPost("register")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Register(RegisterCommand command)
    {
        var result = await sender.Send(command);
        return result.Succeeded ? Ok(new { message = result.Data }) : BadRequest(new { errors = result.Errors });
    }

    [HttpPost("confirm-email")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> ConfirmEmail(ConfirmEmailCommand command)
    {
        var result = await sender.Send(command);
        return result.Succeeded ? Ok(result.Data) : BadRequest(new { errors = result.Errors });
    }

    [HttpPost("login")]
    [EnableRateLimiting("auth-sensitive")]
    public async Task<IActionResult> Login(LoginCommand command)
    {
        var result = await sender.Send(command);
        return result.Succeeded ? Ok(result.Data) : Unauthorized(new { errors = result.Errors });
    }

    [HttpPost("refresh")]
    [EnableRateLimiting("auth")]
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

    [HttpPost("forgot-password")]
    [EnableRateLimiting("auth-sensitive")]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordCommand command)
    {
        var result = await sender.Send(command);
        return Ok(new { message = result.Data });
    }

    [HttpPost("reset-password")]
    [EnableRateLimiting("auth-sensitive")]
    public async Task<IActionResult> ResetPassword(ResetPasswordCommand command)
    {
        var result = await sender.Send(command);
        return result.Succeeded ? Ok(new { message = result.Data }) : BadRequest(new { errors = result.Errors });
    }

    [Authorize]
    [HttpDelete("account")]
    [EnableRateLimiting("auth-sensitive")]
    public async Task<IActionResult> DeleteAccount(DeleteAccountRequest request)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var command = new DeleteAccountCommand(userId, request.Password);
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
