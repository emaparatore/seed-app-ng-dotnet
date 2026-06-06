using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Seed.Application.Common.Interfaces;
using Seed.Domain.Authorization;
using Seed.Shared.Configuration;
using Stripe;

namespace Seed.Api.Controllers;

[ApiController]
[Route("webhooks/stripe")]
public class StripeWebhookController(
    IWebhookEventHandler webhookHandler,
    IOptions<StripeSettings> stripeSettings,
    IAuditService auditService,
    ILogger<StripeWebhookController> logger) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> HandleWebhook(CancellationToken ct)
    {
        using var reader = new StreamReader(HttpContext.Request.Body);
        var json = await reader.ReadToEndAsync(ct);

        Event stripeEvent;
        try
        {
            stripeEvent = EventUtility.ConstructEvent(
                json,
                Request.Headers["Stripe-Signature"],
                stripeSettings.Value.WebhookSecret);
        }
        catch (StripeException ex)
        {
            logger.LogWarning(ex, "Stripe webhook signature verification failed");
            await auditService.LogAsync(
                AuditActions.WebhookVerificationFailed,
                "Webhook",
                details: "Stripe signature verification failed",
                cancellationToken: ct);
            return BadRequest();
        }

        await auditService.LogAsync(
            AuditActions.WebhookReceived,
            "Webhook",
            stripeEvent.Id,
            $"Type: {stripeEvent.Type}",
            cancellationToken: ct);

        try
        {
            var processed = await webhookHandler.ProcessEventAsync(stripeEvent.Id, stripeEvent.Type, json, ct);
            if (!processed)
            {
                await auditService.LogAsync(
                    AuditActions.WebhookProcessingFailed,
                    "Webhook",
                    stripeEvent.Id,
                    $"Type: {stripeEvent.Type} - event ignored or incomplete",
                    cancellationToken: ct);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Stripe webhook processing failed for event {EventId}", stripeEvent.Id);
            await auditService.LogAsync(
                AuditActions.WebhookProcessingFailed,
                "Webhook",
                stripeEvent.Id,
                $"Type: {stripeEvent.Type} - exception during processing",
                cancellationToken: ct);
            throw;
        }

        return Ok();
    }
}
