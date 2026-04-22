using Seed.Application.Admin.SystemHealth.Models;

namespace Seed.Application.Common.Interfaces;

public interface IPaymentsHealthService
{
    Task<PaymentsWebhookStatusDto> GetWebhookStatusAsync(CancellationToken ct = default);
}
