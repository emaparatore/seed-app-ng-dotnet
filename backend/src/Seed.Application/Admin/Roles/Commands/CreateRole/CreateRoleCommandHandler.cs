using MediatR;
using Microsoft.AspNetCore.Identity;
using Seed.Application.Common;
using Seed.Application.Common.Interfaces;
using Seed.Domain.Authorization;
using Seed.Domain.Entities;

namespace Seed.Application.Admin.Roles.Commands.CreateRole;

public sealed class CreateRoleCommandHandler(
    RoleManager<ApplicationRole> roleManager,
    IPermissionService permissionService,
    IAuditService auditService)
    : IRequestHandler<CreateRoleCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateRoleCommand request, CancellationToken cancellationToken)
    {
        if (await roleManager.RoleExistsAsync(request.Name))
            return Result<Guid>.Failure("A role with this name already exists.");

        var role = new ApplicationRole
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Description = request.Description,
            IsSystemRole = false,
            CreatedAt = DateTime.UtcNow
        };

        var createResult = await roleManager.CreateAsync(role);
        if (!createResult.Succeeded)
            return Result<Guid>.Failure(createResult.Errors.Select(e => e.Description).ToArray());

        if (request.PermissionNames.Length > 0)
            await permissionService.SetRolePermissionsAsync(role.Id, request.PermissionNames, cancellationToken);

        await auditService.LogAsync(
            AuditActions.RoleCreated,
            "Role",
            role.Id.ToString(),
            $"Name: {role.Name}, Permissions: {string.Join(", ", request.PermissionNames)}",
            request.CurrentUserId,
            request.IpAddress,
            request.UserAgent,
            cancellationToken);

        return Result<Guid>.Success(role.Id);
    }
}
