using Seed.Application.Admin.SystemHealth.Models;
using Seed.Application.Common.Interfaces;

namespace Seed.Infrastructure.Billing.Services;

public sealed class NullPaymentsHealthService : IPaymentsHealthService
{
    public Task<PaymentsWebhookStatusDto> GetWebhookStatusAsync(CancellationToken ct = default)
    {
        var status = new PaymentsWebhookStatusDto(
            Status: "Healthy",
            Description: "Payments module disabled",
            LastWebhookReceivedAt: null,
            LastFailureAt: null,
            RecentFailuresCount: 0,
            PendingCheckoutsCount: 0,
            StalePendingCheckoutsCount: 0);

        return Task.FromResult(status);
    }
}
