using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Advertified.App.Configuration;
using Advertified.App.Data;
using Advertified.App.Services.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Advertified.App.Services;

public sealed class ResendEmailService : ITemplatedEmailService
{
    private readonly HttpClient _httpClient;
    private readonly AppDbContext _db;
    private readonly ResendOptions _options;
    private readonly ILogger<ResendEmailService> _logger;

    public ResendEmailService(
        HttpClient httpClient,
        AppDbContext db,
        IOptions<ResendOptions> options,
        ILogger<ResendEmailService> logger)
    {
        _httpClient = httpClient;
        _db = db;
        _options = options.Value;
        _logger = logger;
    }

    public async Task SendAsync(
        string templateName,
        string recipientEmail,
        string senderKey,
        IReadOnlyDictionary<string, string?> tokens,
        IReadOnlyCollection<EmailAttachment>? attachments,
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

        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            _logger.LogWarning("Resend API key is not configured. Skipping email send for template {TemplateName}.", templateName);
            return;
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, "emails");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
        request.Content = JsonContent.Create(new ResendSendEmailRequest
        {
            From = fromAddress,
            To = new[] { recipientEmail },
            Subject = subject,
            Html = html,
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
            _logger.LogError(
                "Resend email send failed for template {TemplateName}. Status: {StatusCode}. Body: {Body}",
                templateName,
                (int)response.StatusCode,
                responseBody);

            throw new InvalidOperationException($"Resend email send failed for template '{templateName}'.");
        }
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

        [JsonPropertyName("attachments")]
        public IReadOnlyList<ResendAttachment>? Attachments { get; set; }
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
