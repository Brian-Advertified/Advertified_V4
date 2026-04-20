using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Serialization;
using Advertified.App.Configuration;
using Advertified.App.Data.Entities;
using Advertified.App.Support;
using Microsoft.Extensions.Options;

namespace Advertified.App.Services;

public sealed class ResendEmailTransport
{
    private readonly HttpClient _httpClient;
    private readonly ResendOptions _options;
    private readonly IWebHostEnvironment _environment;

    public ResendEmailTransport(
        HttpClient httpClient,
        IOptions<ResendOptions> options,
        IWebHostEnvironment environment)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _environment = environment;
    }

    public async Task<ResendDispatchOutcome> SendAsync(EmailDeliveryMessage message, CancellationToken cancellationToken)
    {
        var attachments = EmailOutboxPayload.DeserializeAttachments(message.AttachmentsJson);
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            var archivePath = await ArchiveEmailAsync(message, attachments, cancellationToken);
            return ResendDispatchOutcome.Archived(archivePath);
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, "emails");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
        request.Headers.TryAddWithoutValidation("Idempotency-Key", $"email-delivery-{message.Id:D}");
        request.Content = JsonContent.Create(new ResendSendEmailRequest
        {
            From = message.FromAddress,
            To = new[] { message.RecipientEmail },
            Subject = message.Subject,
            Html = message.BodyHtml,
            Tags = BuildTags(message),
            Attachments = attachments.Select(attachment => new ResendAttachment
            {
                FileName = attachment.FileName,
                Content = Convert.ToBase64String(attachment.Content),
                ContentType = attachment.ContentType
            }).ToArray()
        });

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            var errorMessage = $"HTTP {(int)response.StatusCode}: {responseBody}";
            return IsRetryable(response.StatusCode)
                ? ResendDispatchOutcome.RetryableFailure(errorMessage)
                : ResendDispatchOutcome.PermanentFailure(errorMessage);
        }

        var sendResponse = await response.Content.ReadFromJsonAsync<ResendSendEmailResponse>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Resend send response was empty.");
        if (string.IsNullOrWhiteSpace(sendResponse.Id))
        {
            throw new InvalidOperationException("Resend send response did not include an email id.");
        }

        return ResendDispatchOutcome.Accepted(sendResponse.Id, null);
    }

    private async Task<string> ArchiveEmailAsync(
        EmailDeliveryMessage message,
        IReadOnlyCollection<Services.Abstractions.EmailAttachment> attachments,
        CancellationToken cancellationToken)
    {
        var relativeDirectory = string.IsNullOrWhiteSpace(_options.LocalArchiveDirectory)
            ? Path.Combine("App_Data", "email_outbox")
            : _options.LocalArchiveDirectory.Trim();
        var archiveDirectory = Path.IsPathRooted(relativeDirectory)
            ? relativeDirectory
            : Path.Combine(_environment.ContentRootPath, relativeDirectory);

        Directory.CreateDirectory(archiveDirectory);

        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmssfff");
        var slug = SanitizeFileSegment($"{message.TemplateName}-{message.RecipientEmail}");
        var folderPath = Path.Combine(archiveDirectory, $"{timestamp}-{slug}");
        Directory.CreateDirectory(folderPath);

        var htmlPath = Path.Combine(folderPath, "message.html");
        await File.WriteAllTextAsync(htmlPath, message.BodyHtml, Encoding.UTF8, cancellationToken);

        var metadata = new StringBuilder()
            .AppendLine($"Template: {message.TemplateName}")
            .AppendLine($"To: {message.RecipientEmail}")
            .AppendLine($"Subject: {message.Subject}")
            .AppendLine($"ArchivedAtUtc: {DateTime.UtcNow:O}")
            .AppendLine($"Attachments: {attachments.Count}");
        await File.WriteAllTextAsync(Path.Combine(folderPath, "metadata.txt"), metadata.ToString(), Encoding.UTF8, cancellationToken);

        foreach (var attachment in attachments)
        {
            var safeFileName = SanitizeFileSegment(attachment.FileName);
            var attachmentPath = Path.Combine(folderPath, safeFileName);
            await File.WriteAllBytesAsync(attachmentPath, attachment.Content, cancellationToken);
        }

        return folderPath;
    }

    private static bool IsRetryable(System.Net.HttpStatusCode statusCode)
    {
        var numeric = (int)statusCode;
        return numeric == 408 || numeric == 429 || numeric >= 500;
    }

    private static IReadOnlyList<ResendTag> BuildTags(EmailDeliveryMessage message)
    {
        var tags = new List<ResendTag>
        {
            new() { Name = "template", Value = SanitizeTagValue(message.TemplateName) }
        };

        if (!string.IsNullOrWhiteSpace(message.DeliveryPurpose))
        {
            tags.Add(new ResendTag { Name = "purpose", Value = SanitizeTagValue(message.DeliveryPurpose) });
        }

        if (message.CampaignId is Guid campaignId)
        {
            tags.Add(new ResendTag { Name = "campaign_id", Value = SanitizeTagValue(campaignId.ToString("D")) });
        }

        if (message.RecommendationRevisionNumber is int revisionNumber)
        {
            tags.Add(new ResendTag { Name = "recommendation_revision", Value = SanitizeTagValue(revisionNumber.ToString()) });
        }

        return tags;
    }

    private static string SanitizeFileSegment(string value)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(value.Length);

        foreach (var character in value)
        {
            builder.Append(invalidChars.Contains(character) ? '_' : character);
        }

        return builder.ToString().Replace(' ', '_');
    }

    private static string SanitizeTagValue(string value)
    {
        var trimmed = value.Trim();
        var builder = new StringBuilder(trimmed.Length);
        foreach (var character in trimmed)
        {
            builder.Append(char.IsLetterOrDigit(character) || character is '_' or '-' ? character : '-');
        }

        return builder.ToString().Trim('-');
    }

    private sealed class ResendSendEmailRequest
    {
        [JsonPropertyName("from")]
        public string From { get; set; } = string.Empty;

        [JsonPropertyName("to")]
        public IReadOnlyList<string> To { get; set; } = Array.Empty<string>();

        [JsonPropertyName("subject")]
        public string Subject { get; set; } = string.Empty;

        [JsonPropertyName("html")]
        public string Html { get; set; } = string.Empty;

        [JsonPropertyName("tags")]
        public IReadOnlyList<ResendTag>? Tags { get; set; }

        [JsonPropertyName("attachments")]
        public IReadOnlyList<ResendAttachment>? Attachments { get; set; }
    }

    private sealed class ResendSendEmailResponse
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;
    }

    private sealed class ResendTag
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("value")]
        public string Value { get; set; } = string.Empty;
    }

    private sealed class ResendAttachment
    {
        [JsonPropertyName("filename")]
        public string FileName { get; set; } = string.Empty;

        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;

        [JsonPropertyName("content_type")]
        public string ContentType { get; set; } = "application/octet-stream";
    }
}

public sealed record ResendDispatchOutcome(
    string Outcome,
    string? ProviderMessageId,
    string? ProviderBroadcastId,
    string? ArchivePath,
    string? ErrorMessage)
{
    public static ResendDispatchOutcome Accepted(string providerMessageId, string? providerBroadcastId)
        => new("accepted", providerMessageId, providerBroadcastId, null, null);

    public static ResendDispatchOutcome Archived(string archivePath)
        => new("archived", null, null, archivePath, null);

    public static ResendDispatchOutcome RetryableFailure(string errorMessage)
        => new("retryable_failure", null, null, null, errorMessage);

    public static ResendDispatchOutcome PermanentFailure(string errorMessage)
        => new("permanent_failure", null, null, null, errorMessage);
}
