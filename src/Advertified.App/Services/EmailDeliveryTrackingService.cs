using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Advertified.App.Data;
using Advertified.App.Data.Entities;
using Advertified.App.Services.Abstractions;
using Advertified.App.Support;
using Microsoft.EntityFrameworkCore;

namespace Advertified.App.Services;

public sealed class EmailDeliveryTrackingService : IEmailDeliveryTrackingService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly AppDbContext _db;
    private readonly IEmailIntegrationSecretCipher _secretCipher;
    private readonly ILogger<EmailDeliveryTrackingService> _logger;

    public EmailDeliveryTrackingService(
        AppDbContext db,
        IEmailIntegrationSecretCipher secretCipher,
        ILogger<EmailDeliveryTrackingService> logger)
    {
        _db = db;
        _secretCipher = secretCipher;
        _logger = logger;
    }

    public async Task<TrackedEmailDispatch> CreatePendingDispatchAsync(
        string providerKey,
        string templateName,
        string senderKey,
        string fromAddress,
        string recipientEmail,
        string subject,
        EmailTrackingContext? trackingContext,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var message = new EmailDeliveryMessage
        {
            Id = Guid.NewGuid(),
            ProviderKey = providerKey.Trim(),
            TemplateName = templateName.Trim(),
            SenderKey = senderKey.Trim(),
            DeliveryPurpose = string.IsNullOrWhiteSpace(trackingContext?.Purpose) ? templateName.Trim() : trackingContext.Purpose.Trim(),
            Status = EmailDeliveryStatuses.Pending,
            FromAddress = fromAddress.Trim(),
            RecipientEmail = recipientEmail.Trim(),
            Subject = subject.Trim(),
            CampaignId = trackingContext?.CampaignId,
            RecommendationId = trackingContext?.RecommendationId,
            RecommendationRevisionNumber = trackingContext?.RecommendationRevisionNumber,
            RecipientUserId = trackingContext?.RecipientUserId,
            ProspectLeadId = trackingContext?.ProspectLeadId,
            MetadataJson = SerializeMetadata(trackingContext?.Metadata),
            CreatedAt = now,
            UpdatedAt = now
        };

        _db.EmailDeliveryMessages.Add(message);
        await _db.SaveChangesAsync(cancellationToken);

        return new TrackedEmailDispatch
        {
            DispatchId = message.Id,
            IdempotencyKey = $"email-delivery-{message.Id:D}"
        };
    }

    public async Task MarkAcceptedAsync(
        Guid dispatchId,
        string providerMessageId,
        string? providerBroadcastId,
        CancellationToken cancellationToken)
    {
        var message = await _db.EmailDeliveryMessages.FirstOrDefaultAsync(x => x.Id == dispatchId, cancellationToken)
            ?? throw new InvalidOperationException("Email delivery dispatch not found.");

        var now = DateTime.UtcNow;
        message.ProviderMessageId = providerMessageId.Trim();
        message.ProviderBroadcastId = Normalize(providerBroadcastId);
        message.Status = EmailDeliveryStatuses.Accepted;
        message.AcceptedAt = now;
        message.LatestEventType ??= "email.sent";
        message.LatestEventAt ??= now;
        message.LastError = null;
        message.UpdatedAt = now;

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkArchivedAsync(Guid dispatchId, string archivePath, CancellationToken cancellationToken)
    {
        var message = await _db.EmailDeliveryMessages.FirstOrDefaultAsync(x => x.Id == dispatchId, cancellationToken)
            ?? throw new InvalidOperationException("Email delivery dispatch not found.");

        var now = DateTime.UtcNow;
        message.Status = EmailDeliveryStatuses.Archived;
        message.ArchivedAt = now;
        message.ArchivedPath = archivePath;
        message.LatestEventType = "archived";
        message.LatestEventAt = now;
        message.UpdatedAt = now;

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkFailedAsync(Guid dispatchId, string errorMessage, CancellationToken cancellationToken)
    {
        var message = await _db.EmailDeliveryMessages.FirstOrDefaultAsync(x => x.Id == dispatchId, cancellationToken)
            ?? throw new InvalidOperationException("Email delivery dispatch not found.");

        var now = DateTime.UtcNow;
        message.Status = EmailDeliveryStatuses.Failed;
        message.FailedAt = now;
        message.LatestEventType = "failed";
        message.LatestEventAt = now;
        message.LastError = errorMessage;
        message.UpdatedAt = now;

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<EmailWebhookProcessResult> ProcessResendWebhookAsync(
        string requestPath,
        IReadOnlyDictionary<string, string> headers,
        string payload,
        CancellationToken cancellationToken)
    {
        const string providerKey = "resend";
        var webhookMessageId = GetHeader(headers, "svix-id");
        var timestampValue = GetHeader(headers, "svix-timestamp");
        var signatureHeader = GetHeader(headers, "svix-signature");
        var audit = new EmailDeliveryWebhookAudit
        {
            Id = Guid.NewGuid(),
            ProviderKey = providerKey,
            RequestPath = requestPath,
            WebhookMessageId = webhookMessageId,
            HeadersJson = JsonSerializer.Serialize(headers, JsonOptions),
            PayloadJson = TryNormalizeJson(payload),
            ProcessingStatus = "received",
            CreatedAt = DateTime.UtcNow
        };

        _db.EmailDeliveryWebhookAudits.Add(audit);
        await _db.SaveChangesAsync(cancellationToken);

        var provider = await _db.EmailDeliveryProviderSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.ProviderKey == providerKey, cancellationToken);

        if (provider is null || !provider.WebhookEnabled)
        {
            return await CompleteAuditAsync(audit, false, "ignored", "Webhook is not enabled for provider.", cancellationToken);
        }

        var signingSecret = _secretCipher.Unprotect(provider.WebhookSigningSecret);
        if (string.IsNullOrWhiteSpace(signingSecret))
        {
            return await CompleteAuditAsync(audit, false, "rejected", "Webhook signing secret is not configured.", cancellationToken);
        }

        if (!TryVerifySignature(signingSecret, webhookMessageId, timestampValue, signatureHeader, payload, provider.MaxSignatureAgeSeconds, out var verificationError))
        {
            return await CompleteAuditAsync(audit, false, "rejected", verificationError, cancellationToken);
        }

        ResendWebhookEnvelope? envelope;
        try
        {
            envelope = JsonSerializer.Deserialize<ResendWebhookEnvelope>(payload, JsonOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Invalid Resend webhook payload received.");
            return await CompleteAuditAsync(audit, true, "rejected", "Payload is not valid JSON.", cancellationToken);
        }

        if (envelope is null || string.IsNullOrWhiteSpace(envelope.Type) || envelope.Data is null)
        {
            return await CompleteAuditAsync(audit, true, "rejected", "Payload does not contain the required Resend event fields.", cancellationToken);
        }

        audit.EventType = envelope.Type.Trim();
        await _db.SaveChangesAsync(cancellationToken);

        var allowedEvents = DeserializeStringList(provider.AllowedEventTypesJson);
        if (allowedEvents.Count > 0 && !allowedEvents.Contains(envelope.Type, StringComparer.OrdinalIgnoreCase))
        {
            return await CompleteAuditAsync(audit, true, "ignored", "Event type is not subscribed in provider settings.", cancellationToken);
        }

        var duplicate = !string.IsNullOrWhiteSpace(webhookMessageId)
            && await _db.EmailDeliveryEvents.AsNoTracking()
                .AnyAsync(
                    x => x.ProviderKey == providerKey && x.ProviderWebhookMessageId == webhookMessageId,
                    cancellationToken);
        if (duplicate)
        {
            return await CompleteAuditAsync(audit, true, "duplicate", "Duplicate webhook delivery ignored.", cancellationToken, duplicate: true);
        }

        var emailId = Normalize(envelope.Data.EmailId);
        var message = string.IsNullOrWhiteSpace(emailId)
            ? null
            : await _db.EmailDeliveryMessages.FirstOrDefaultAsync(
                x => x.ProviderKey == providerKey && x.ProviderMessageId == emailId,
                cancellationToken);

        var now = DateTime.UtcNow;
        var eventCreatedAt = envelope.CreatedAt ?? envelope.Data.CreatedAt ?? now;
        var deliveryEvent = new EmailDeliveryEvent
        {
            Id = Guid.NewGuid(),
            ProviderKey = providerKey,
            EmailDeliveryMessageId = message?.Id,
            ProviderWebhookMessageId = webhookMessageId,
            ProviderMessageId = emailId,
            ProviderEventType = envelope.Type.Trim(),
            RecipientEmail = envelope.Data.To?.FirstOrDefault(),
            EventCreatedAt = eventCreatedAt,
            ReceivedAt = now,
            ProcessingStatus = message is null ? "unmatched" : "processed",
            ProcessingNotes = message is null ? "No tracked outbound email matched the provider message id." : null,
            PayloadJson = TryNormalizeJson(payload) ?? payload
        };
        _db.EmailDeliveryEvents.Add(deliveryEvent);

        if (message is not null)
        {
            ApplyEvent(message, envelope, eventCreatedAt);
        }

        audit.SignatureValid = true;
        audit.ProcessingStatus = message is null ? "unmatched" : "processed";
        audit.ProcessingNotes = message is null ? "No tracked outbound email matched the provider message id." : null;
        audit.ProcessedAt = now;

        await _db.SaveChangesAsync(cancellationToken);

        return new EmailWebhookProcessResult
        {
            SignatureValid = true,
            Duplicate = false,
            ProcessingStatus = audit.ProcessingStatus,
            ProcessingNotes = audit.ProcessingNotes,
            EventType = envelope.Type,
            WebhookMessageId = webhookMessageId
        };
    }

    private async Task<EmailWebhookProcessResult> CompleteAuditAsync(
        EmailDeliveryWebhookAudit audit,
        bool signatureValid,
        string status,
        string notes,
        CancellationToken cancellationToken,
        bool duplicate = false)
    {
        audit.SignatureValid = signatureValid;
        audit.ProcessingStatus = status;
        audit.ProcessingNotes = notes;
        audit.ProcessedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        return new EmailWebhookProcessResult
        {
            SignatureValid = signatureValid,
            Duplicate = duplicate,
            ProcessingStatus = status,
            ProcessingNotes = notes,
            EventType = audit.EventType,
            WebhookMessageId = audit.WebhookMessageId
        };
    }

    private static void ApplyEvent(EmailDeliveryMessage message, ResendWebhookEnvelope envelope, DateTime eventCreatedAt)
    {
        var data = envelope.Data ?? new ResendWebhookData();
        message.ProviderBroadcastId ??= Normalize(data.BroadcastId);
        message.LatestEventType = envelope.Type;
        message.LatestEventAt = eventCreatedAt;
        message.UpdatedAt = DateTime.UtcNow;

        switch (envelope.Type)
        {
            case "email.sent":
                message.Status = message.Status == EmailDeliveryStatuses.Delivered ? message.Status : EmailDeliveryStatuses.Accepted;
                message.AcceptedAt ??= eventCreatedAt;
                break;
            case "email.delivered":
                message.Status = EmailDeliveryStatuses.Delivered;
                message.DeliveredAt ??= eventCreatedAt;
                break;
            case "email.delivery_delayed":
                if (message.Status is not EmailDeliveryStatuses.Delivered and not EmailDeliveryStatuses.Bounced and not EmailDeliveryStatuses.Failed)
                {
                    message.Status = EmailDeliveryStatuses.DeliveryDelayed;
                }
                break;
            case "email.opened":
                message.OpenedAt ??= eventCreatedAt;
                if (message.Status == EmailDeliveryStatuses.Accepted)
                {
                    message.Status = EmailDeliveryStatuses.Delivered;
                }
                break;
            case "email.clicked":
                message.ClickedAt ??= eventCreatedAt;
                if (message.Status == EmailDeliveryStatuses.Accepted)
                {
                    message.Status = EmailDeliveryStatuses.Delivered;
                }
                break;
            case "email.complained":
                message.ComplainedAt ??= eventCreatedAt;
                message.Status = EmailDeliveryStatuses.Complained;
                message.LastError = Normalize(data.Complaint?.Message);
                break;
            case "email.bounced":
                message.BouncedAt ??= eventCreatedAt;
                message.Status = EmailDeliveryStatuses.Bounced;
                message.LastError = BuildBounceMessage(data.Bounce);
                break;
            case "email.failed":
                message.FailedAt ??= eventCreatedAt;
                message.Status = EmailDeliveryStatuses.Failed;
                message.LastError = Normalize(data.FailureMessage) ?? "Provider reported a send failure.";
                break;
        }
    }

    private static string BuildBounceMessage(ResendBounceData? bounce)
    {
        if (bounce is null)
        {
            return "Provider reported a bounce.";
        }

        return string.Join(" | ", new[] { Normalize(bounce.Type), Normalize(bounce.SubType), Normalize(bounce.Message) }.Where(x => !string.IsNullOrWhiteSpace(x)));
    }

    private static string? SerializeMetadata(IReadOnlyDictionary<string, string?>? metadata)
    {
        if (metadata is null || metadata.Count == 0)
        {
            return null;
        }

        return JsonSerializer.Serialize(metadata, JsonOptions);
    }

    private static HashSet<string> DeserializeStringList(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<List<string>>(value, JsonOptions) ?? new List<string>();
            return parsed.Where(x => !string.IsNullOrWhiteSpace(x)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static bool TryVerifySignature(
        string signingSecret,
        string? webhookMessageId,
        string? timestampValue,
        string? signatureHeader,
        string payload,
        int maxSignatureAgeSeconds,
        out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(webhookMessageId) || string.IsNullOrWhiteSpace(timestampValue) || string.IsNullOrWhiteSpace(signatureHeader))
        {
            error = "Required Svix headers are missing.";
            return false;
        }

        if (!long.TryParse(timestampValue, out var timestampSeconds))
        {
            error = "Webhook timestamp header is invalid.";
            return false;
        }

        var nowSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (maxSignatureAgeSeconds > 0 && Math.Abs(nowSeconds - timestampSeconds) > maxSignatureAgeSeconds)
        {
            error = "Webhook timestamp is outside the allowed verification window.";
            return false;
        }

        var secretPayload = signingSecret.StartsWith("whsec_", StringComparison.Ordinal)
            ? signingSecret["whsec_".Length..]
            : signingSecret;

        byte[] secretBytes;
        try
        {
            secretBytes = Convert.FromBase64String(secretPayload);
        }
        catch (FormatException)
        {
            error = "Webhook signing secret is not valid base64.";
            return false;
        }

        var signedContent = $"{webhookMessageId}.{timestampValue}.{payload}";
        using var hmac = new HMACSHA256(secretBytes);
        var expected = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(signedContent)));
        var expectedBytes = Encoding.UTF8.GetBytes(expected);

        foreach (var segment in signatureHeader.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!segment.StartsWith("v1,", StringComparison.Ordinal))
            {
                continue;
            }

            var actual = segment["v1,".Length..];
            var actualBytes = Encoding.UTF8.GetBytes(actual);
            if (CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes))
            {
                return true;
            }
        }

        error = "Webhook signature verification failed.";
        return false;
    }

    private static string? GetHeader(IReadOnlyDictionary<string, string> headers, string key)
    {
        return headers.TryGetValue(key, out var value) ? value : null;
    }

    private static string? TryNormalizeJson(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(payload);
            return JsonSerializer.Serialize(document, JsonOptions);
        }
        catch
        {
            return payload;
        }
    }

    private static string? Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private sealed class ResendWebhookEnvelope
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        [JsonPropertyName("created_at")]
        public DateTime? CreatedAt { get; set; }

        [JsonPropertyName("data")]
        public ResendWebhookData? Data { get; set; }
    }

    private sealed class ResendWebhookData
    {
        [JsonPropertyName("email_id")]
        public string? EmailId { get; set; }

        [JsonPropertyName("broadcast_id")]
        public string? BroadcastId { get; set; }

        [JsonPropertyName("created_at")]
        public DateTime? CreatedAt { get; set; }

        [JsonPropertyName("to")]
        public List<string>? To { get; set; }

        [JsonPropertyName("bounce")]
        public ResendBounceData? Bounce { get; set; }

        [JsonPropertyName("complaint")]
        public ResendComplaintData? Complaint { get; set; }

        [JsonPropertyName("failure_message")]
        public string? FailureMessage { get; set; }
    }

    private sealed class ResendBounceData
    {
        [JsonPropertyName("message")]
        public string? Message { get; set; }

        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("subType")]
        public string? SubType { get; set; }
    }

    private sealed class ResendComplaintData
    {
        [JsonPropertyName("message")]
        public string? Message { get; set; }
    }
}
