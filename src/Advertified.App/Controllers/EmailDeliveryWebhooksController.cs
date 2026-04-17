using System.Text;
using Advertified.App.Services.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Advertified.App.Controllers;

[ApiController]
[Route("webhooks/email-delivery")]
[AllowAnonymous]
public sealed class EmailDeliveryWebhooksController : ControllerBase
{
    private readonly IEmailDeliveryTrackingService _trackingService;

    public EmailDeliveryWebhooksController(IEmailDeliveryTrackingService trackingService)
    {
        _trackingService = trackingService;
    }

    [HttpPost("resend")]
    public async Task<IActionResult> ReceiveResendWebhook(CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(Request.Body, Encoding.UTF8);
        var payload = await reader.ReadToEndAsync(cancellationToken);
        var headers = Request.Headers.ToDictionary(
            header => header.Key,
            header => header.Value.ToString(),
            StringComparer.OrdinalIgnoreCase);

        var result = await _trackingService.ProcessResendWebhookAsync(Request.Path, headers, payload, cancellationToken);
        return result.ProcessingStatus switch
        {
            "processed" => Ok(new { message = "Webhook processed." }),
            "duplicate" => Ok(new { message = "Duplicate webhook ignored." }),
            "unmatched" => Ok(new { message = "Webhook stored without a matching outbound email." }),
            "ignored" => Ok(new { message = result.ProcessingNotes }),
            "rejected" => Unauthorized(new { message = result.ProcessingNotes }),
            _ => Ok(new { message = "Webhook received." })
        };
    }
}
