using MediatR;
using Microsoft.AspNetCore.Identity;
using Seed.Application.Common;
using Seed.Application.Common.Interfaces;
using Seed.Domain.Authorization;
using Seed.Domain.Entities;

namespace Seed.Application.Admin.Users.Commands.CreateUser;

public sealed class CreateUserCommandHandler(
    UserManager<ApplicationUser> userManager,
    RoleManager<ApplicationRole> roleManager,
    IAuditService auditService)
    : IRequestHandler<CreateUserCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateUserCommand request, CancellationToken cancellationToken)
    {
        var existingUser = await userManager.FindByEmailAsync(request.Email);
        if (existingUser is not null)
            return Result<Guid>.Failure("A user with this email already exists.");

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = request.Email,
            Email = request.Email,
            FirstName = request.FirstName,
            LastName = request.LastName,
            EmailConfirmed = true,
            IsActive = true,
            MustChangePassword = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var createResult = await userManager.CreateAsync(user, request.Password);
        if (!createResult.Succeeded)
            return Result<Guid>.Failure(createResult.Errors.Select(e => e.Description).ToArray());

        // Assign roles
        foreach (var roleName in request.RoleNames)
        {
            if (await roleManager.RoleExistsAsync(roleName))
                await userManager.AddToRoleAsync(user, roleName);
        }

        await auditService.LogAsync(
            AuditActions.UserCreated,
            "User",
            user.Id.ToString(),
            $"Email: {user.Email}, Roles: {string.Join(", ", request.RoleNames)}",
            request.CurrentUserId,
            request.IpAddress,
            request.UserAgent,
            cancellationToken);

        return Result<Guid>.Success(user.Id);
    }
}
