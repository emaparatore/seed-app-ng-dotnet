using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Seed.Domain.Entities;

namespace Seed.Api.Middleware;

public sealed class MustChangePasswordMiddleware(RequestDelegate next)
{
    private static readonly HashSet<string> ExcludedPaths =
    [
        "/auth/change-password",
        "/auth/logout",
        "/auth/refresh"
    ];

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var path = context.Request.Path.Value?.ToLowerInvariant() ?? "";

            var isExcluded = ExcludedPaths.Any(excluded => path.Contains(excluded));

            if (!isExcluded)
            {
                var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (userId is not null)
                {
                    var userManager = context.RequestServices.GetRequiredService<UserManager<ApplicationUser>>();
                    var user = await userManager.FindByIdAsync(userId);

                    if (user is not null && user.MustChangePassword)
                    {
                        context.Response.StatusCode = StatusCodes.Status403Forbidden;
                        context.Response.ContentType = "application/json";
                        await context.Response.WriteAsJsonAsync(new
                        {
                            type = "PASSWORD_CHANGE_REQUIRED",
                            title = "Password change required",
                            status = 403
                        });
                        return;
                    }
                }
            }
        }

        await next(context);
    }
}
