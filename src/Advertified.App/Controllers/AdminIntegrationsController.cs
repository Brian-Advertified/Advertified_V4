using Advertified.App.Contracts.Admin;
using Advertified.App.Data;
using Advertified.App.Services;
using Advertified.App.Services.Abstractions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Advertified.App.Controllers;

[ApiController]
[Route("admin/integrations")]
public sealed class AdminIntegrationsController : BaseAdminController
{
    private readonly IEmailIntegrationSecretCipher _secretCipher;
    private readonly AdminIntegrationStatusService _integrationStatusService;

    public AdminIntegrationsController(
        AppDbContext db,
        ICurrentUserAccessor currentUserAccessor,
        IChangeAuditService changeAuditService,
        IEmailIntegrationSecretCipher secretCipher,
        AdminIntegrationStatusService integrationStatusService)
        : base(db, currentUserAccessor, changeAuditService)
    {
        _secretCipher = secretCipher;
        _integrationStatusService = integrationStatusService;
    }

    [HttpGet("")]
    public async Task<ActionResult<AdminIntegrationStatusResponse>> GetIntegrationStatus(CancellationToken cancellationToken)
    {
        var gateResult = await EnsureAdminAsync(cancellationToken);
        if (gateResult is not null)
        {
            return gateResult;
        }

        return Ok(await _integrationStatusService.GetAsync(cancellationToken));
    }

    [HttpGet("email-delivery/providers/{providerKey}")]
    public async Task<ActionResult<AdminEmailDeliveryProviderSettingResponse>> GetEmailDeliveryProviderSetting(string providerKey, CancellationToken cancellationToken)
    {
        var gateResult = await EnsureAdminAsync(cancellationToken);
        if (gateResult is not null)
        {
            return gateResult;
        }

        var setting = await Db.EmailDeliveryProviderSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.ProviderKey == providerKey, cancellationToken);
        if (setting is null)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Email delivery provider setting not found.",
                Status = StatusCodes.Status404NotFound
            });
        }

        return Ok(MapEmailDeliveryProvider(setting));
    }

    [HttpPut("email-delivery/providers/{providerKey}")]
    public async Task<ActionResult<AdminEmailDeliveryProviderSettingResponse>> UpdateEmailDeliveryProviderSetting(
        string providerKey,
        [FromBody] UpdateAdminEmailDeliveryProviderSettingRequest request,
        CancellationToken cancellationToken)
    {
        var gateResult = await EnsureAdminAsync(cancellationToken);
        if (gateResult is not null)
        {
            return gateResult;
        }

        var setting = await Db.EmailDeliveryProviderSettings.FirstOrDefaultAsync(x => x.ProviderKey == providerKey, cancellationToken);
        if (setting is null)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Email delivery provider setting not found.",
                Status = StatusCodes.Status404NotFound
            });
        }

        if (request.MaxSignatureAgeSeconds <= 0 || request.MaxSignatureAgeSeconds > 3600)
        {
            throw new InvalidOperationException("Signature age window must be between 1 and 3600 seconds.");
        }

        var allowedEventTypes = (request.AllowedEventTypes ?? Array.Empty<string>())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        setting.WebhookEnabled = request.WebhookEnabled;
        setting.WebhookEndpointPath = TrimOrNull(request.WebhookEndpointPath);
        setting.AllowedEventTypesJson = System.Text.Json.JsonSerializer.Serialize(allowedEventTypes);
        setting.MaxSignatureAgeSeconds = request.MaxSignatureAgeSeconds;
        if (!string.IsNullOrWhiteSpace(request.WebhookSigningSecret))
        {
            setting.WebhookSigningSecret = _secretCipher.Protect(request.WebhookSigningSecret);
        }

        setting.UpdatedAt = DateTime.UtcNow;
        await Db.SaveChangesAsync(cancellationToken);
        await WriteChangeAuditAsync(
            "update_email_delivery_provider",
            "email_delivery_provider_setting",
            setting.ProviderKey,
            setting.DisplayName,
            $"Updated email delivery provider setting for {setting.DisplayName}.",
            new
            {
                setting.ProviderKey,
                setting.WebhookEnabled,
                setting.WebhookEndpointPath,
                setting.MaxSignatureAgeSeconds,
                AllowedEventCount = allowedEventTypes.Length,
                SigningSecretUpdated = !string.IsNullOrWhiteSpace(request.WebhookSigningSecret)
            },
            cancellationToken);

        return Ok(MapEmailDeliveryProvider(setting));
    }

    private static AdminEmailDeliveryProviderSettingResponse MapEmailDeliveryProvider(Data.Entities.EmailDeliveryProviderSetting setting)
    {
        return new AdminEmailDeliveryProviderSettingResponse
        {
            ProviderKey = setting.ProviderKey,
            DisplayName = setting.DisplayName,
            WebhookEnabled = setting.WebhookEnabled,
            HasWebhookSigningSecret = !string.IsNullOrWhiteSpace(setting.WebhookSigningSecret),
            WebhookEndpointPath = setting.WebhookEndpointPath,
            AllowedEventTypes = DeserializeList(setting.AllowedEventTypesJson),
            MaxSignatureAgeSeconds = setting.MaxSignatureAgeSeconds,
            UpdatedAt = setting.UpdatedAt
        };
    }
}
