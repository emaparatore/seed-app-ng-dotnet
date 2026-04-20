namespace Seed.Application.Common.Interfaces;

public interface IWebhookEventHandler
{
    Task<bool> ProcessEventAsync(string eventId, string eventType, string jsonPayload, CancellationToken ct = default);
}
