using MediatR;
using Microsoft.EntityFrameworkCore;
using Seed.Application.Admin.Plans.Commands.ArchivePlan;
using Seed.Application.Common;
using Seed.Application.Common.Interfaces;
using Seed.Domain.Authorization;
using Seed.Domain.Enums;
using Seed.Infrastructure.Persistence;

namespace Seed.Infrastructure.Billing.Commands;

public sealed class ArchivePlanCommandHandler(
    ApplicationDbContext dbContext,
    IAuditService auditService)
    : IRequestHandler<ArchivePlanCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(
        ArchivePlanCommand request, CancellationToken cancellationToken)
    {
        var plan = await dbContext.SubscriptionPlans
            .FirstOrDefaultAsync(p => p.Id == request.PlanId, cancellationToken);

        if (plan is null)
            return Result<bool>.Failure("Plan not found.");

        plan.Status = PlanStatus.Archived;
        plan.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditService.LogAsync(
            AuditActions.PlanArchived,
            entityType: "SubscriptionPlan",
            entityId: plan.Id.ToString(),
            details: $"Plan '{plan.Name}' archived",
            userId: request.CurrentUserId,
            ipAddress: request.IpAddress,
            userAgent: request.UserAgent,
            cancellationToken: cancellationToken);

        return Result<bool>.Success(true);
    }
}
