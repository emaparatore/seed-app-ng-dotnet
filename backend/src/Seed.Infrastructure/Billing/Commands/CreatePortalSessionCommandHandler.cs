using MediatR;
using Microsoft.EntityFrameworkCore;
using Seed.Application.Billing.Commands.CreatePortalSession;
using Seed.Application.Billing.Models;
using Seed.Application.Common;
using Seed.Application.Common.Interfaces;
using Seed.Infrastructure.Persistence;

namespace Seed.Infrastructure.Billing.Commands;

public sealed class CreatePortalSessionCommandHandler(
    ApplicationDbContext dbContext,
    IPaymentGateway paymentGateway)
    : IRequestHandler<CreatePortalSessionCommand, Result<PortalSessionResponse>>
{
    public async Task<Result<PortalSessionResponse>> Handle(
        CreatePortalSessionCommand request, CancellationToken cancellationToken)
    {
        var stripeCustomerId = await dbContext.UserSubscriptions
            .AsNoTracking()
            .Where(s => s.UserId == request.UserId && s.StripeCustomerId != null)
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => s.StripeCustomerId)
            .FirstOrDefaultAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(stripeCustomerId))
            return Result<PortalSessionResponse>.Failure("No billing account found. Please subscribe to a plan first.");

        var portalUrl = await paymentGateway.CreateCustomerPortalSessionAsync(
            stripeCustomerId, request.ReturnUrl, cancellationToken);

        return Result<PortalSessionResponse>.Success(new PortalSessionResponse(portalUrl));
    }
}
