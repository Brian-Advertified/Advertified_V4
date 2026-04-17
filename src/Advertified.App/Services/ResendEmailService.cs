using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Serialization;
using Advertified.App.Configuration;
using Advertified.App.Data;
using Advertified.App.Services.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Advertified.App.Services;

public sealed class ResendEmailService : ITemplatedEmailService
{
    private const string ProviderKey = "resend";
    private readonly HttpClient _httpClient;
    private readonly AppDbContext _db;
    private readonly ResendOptions _options;
    private readonly IWebHostEnvironment _environment;
    private readonly IEmailDeliveryTrackingService _trackingService;
    private readonly ILogger<ResendEmailService> _logger;

    public ResendEmailService(
        HttpClient httpClient,
        AppDbContext db,
        IOptions<ResendOptions> options,
        IWebHostEnvironment environment,
        IEmailDeliveryTrackingService trackingService,
        ILogger<ResendEmailService> logger)
    {
        _httpClient = httpClient;
        _db = db;
        _options = options.Value;
        _environment = environment;
        _trackingService = trackingService;
        _logger = logger;
    }

    public async Task SendAsync(
        string templateName,
        string recipientEmail,
        string senderKey,
        IReadOnlyDictionary<string, string?> tokens,
        IReadOnlyCollection<EmailAttachment>? attachments,
        EmailTrackingContext? trackingContext,
        CancellationToken cancellationToken)
    {
        var template = await _db.EmailTemplates
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.TemplateName == templateName && x.IsActive,
                cancellationToken)
            ?? throw new InvalidOperationException($"Email template '{templateName}' was not found.");

        var fromAddress = ResolveFromAddress(senderKey);
        var subject = Render(template.SubjectTemplate, tokens);
        var html = Render(template.BodyHtmlTemplate, tokens);
        var trackedDispatch = await _trackingService.CreatePendingDispatchAsync(
            ProviderKey,
            templateName,
            senderKey,
            fromAddress,
            recipientEmail,
            subject,
            trackingContext,
            cancellationToken);

        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            var archivePath = await ArchiveEmailAsync(templateName, recipientEmail, subject, html, attachments, cancellationToken);
            await _trackingService.MarkArchivedAsync(trackedDispatch.DispatchId, archivePath, cancellationToken);
            _logger.LogWarning(
                "Resend API key is not configured. Email for template {TemplateName} was archived locally at {ArchivePath}.",
                templateName,
                archivePath);
            return;
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, "emails");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
        request.Headers.TryAddWithoutValidation("Idempotency-Key", trackedDispatch.IdempotencyKey);
        request.Content = JsonContent.Create(new ResendSendEmailRequest
        {
            From = fromAddress,
            To = new[] { recipientEmail },
            Subject = subject,
            Html = html,
            Tags = BuildTags(templateName, trackingContext),
            Attachments = attachments?.Select(attachment => new ResendAttachment
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
            var archivePath = await ArchiveEmailAsync(templateName, recipientEmail, subject, html, attachments, cancellationToken);
            await _trackingService.MarkFailedAsync(
                trackedDispatch.DispatchId,
                $"HTTP {(int)response.StatusCode}: {responseBody}",
                cancellationToken);
            _logger.LogError(
                "Resend email send failed for template {TemplateName}. Status: {StatusCode}. Body: {Body}. Local archive: {ArchivePath}",
                templateName,
                (int)response.StatusCode,
                responseBody,
                archivePath);

            throw new InvalidOperationException($"Resend email send failed for template '{templateName}'.");
        }

        var sendResponse = await response.Content.ReadFromJsonAsync<ResendSendEmailResponse>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Resend send response was empty.");
        if (string.IsNullOrWhiteSpace(sendResponse.Id))
        {
            throw new InvalidOperationException("Resend send response did not include an email id.");
        }

        await _trackingService.MarkAcceptedAsync(trackedDispatch.DispatchId, sendResponse.Id, null, cancellationToken);
    }

    private async Task<string> ArchiveEmailAsync(
        string templateName,
        string recipientEmail,
        string subject,
        string html,
        IReadOnlyCollection<EmailAttachment>? attachments,
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
        var slug = SanitizeFileSegment($"{templateName}-{recipientEmail}");
        var folderPath = Path.Combine(archiveDirectory, $"{timestamp}-{slug}");
        Directory.CreateDirectory(folderPath);

        var htmlPath = Path.Combine(folderPath, "message.html");
        await File.WriteAllTextAsync(htmlPath, html, Encoding.UTF8, cancellationToken);

        var metadata = new StringBuilder()
            .AppendLine($"Template: {templateName}")
            .AppendLine($"To: {recipientEmail}")
            .AppendLine($"Subject: {subject}")
            .AppendLine($"ArchivedAtUtc: {DateTime.UtcNow:O}")
            .AppendLine($"Attachments: {attachments?.Count ?? 0}");
        await File.WriteAllTextAsync(Path.Combine(folderPath, "metadata.txt"), metadata.ToString(), Encoding.UTF8, cancellationToken);

        if (attachments is not null)
        {
            foreach (var attachment in attachments)
            {
                var safeFileName = SanitizeFileSegment(attachment.FileName);
                var attachmentPath = Path.Combine(folderPath, safeFileName);
                await File.WriteAllBytesAsync(attachmentPath, attachment.Content, cancellationToken);
            }
        }

        return folderPath;
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

    private string ResolveFromAddress(string senderKey)
    {
        if (_options.SenderAddresses.TryGetValue(senderKey, out var senderAddress) &&
            !string.IsNullOrWhiteSpace(senderAddress))
        {
            return senderAddress;
        }

        if (!string.IsNullOrWhiteSpace(_options.FromEmail))
        {
            return _options.FromEmail;
        }

        throw new InvalidOperationException($"No sender address is configured for key '{senderKey}'.");
    }

    private static string Render(string template, IReadOnlyDictionary<string, string?> tokens)
    {
        var rendered = template;

        foreach (var token in tokens)
        {
            rendered = rendered.Replace(
                "{{" + token.Key + "}}",
                token.Value ?? string.Empty,
                StringComparison.Ordinal);
        }

        return rendered;
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

    private static IReadOnlyList<ResendTag> BuildTags(string templateName, EmailTrackingContext? trackingContext)
    {
        var tags = new List<ResendTag>
        {
            new() { Name = "template", Value = SanitizeTagValue(templateName) }
        };

        if (!string.IsNullOrWhiteSpace(trackingContext?.Purpose))
        {
            tags.Add(new ResendTag { Name = "purpose", Value = SanitizeTagValue(trackingContext.Purpose) });
        }

        if (trackingContext?.CampaignId is Guid campaignId)
        {
            tags.Add(new ResendTag { Name = "campaign_id", Value = SanitizeTagValue(campaignId.ToString("D")) });
        }

        if (trackingContext?.RecommendationRevisionNumber is int revisionNumber)
        {
            tags.Add(new ResendTag { Name = "recommendation_revision", Value = SanitizeTagValue(revisionNumber.ToString()) });
        }

        return tags;
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
