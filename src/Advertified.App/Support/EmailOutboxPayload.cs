using System.Text.Json;
using Advertified.App.Services.Abstractions;

namespace Advertified.App.Support;

internal static class EmailOutboxPayload
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static string? SerializeAttachments(IReadOnlyCollection<EmailAttachment>? attachments)
    {
        if (attachments is null || attachments.Count == 0)
        {
            return null;
        }

        return JsonSerializer.Serialize(
            attachments.Select(attachment => new EmailAttachmentPayload
            {
                FileName = attachment.FileName,
                ContentType = attachment.ContentType,
                ContentBase64 = Convert.ToBase64String(attachment.Content)
            }),
            JsonOptions);
    }

    public static IReadOnlyList<EmailAttachment> DeserializeAttachments(string? attachmentsJson)
    {
        if (string.IsNullOrWhiteSpace(attachmentsJson))
        {
            return Array.Empty<EmailAttachment>();
        }

        try
        {
            var payloads = JsonSerializer.Deserialize<List<EmailAttachmentPayload>>(attachmentsJson, JsonOptions)
                ?? new List<EmailAttachmentPayload>();
            return payloads
                .Where(payload => !string.IsNullOrWhiteSpace(payload.FileName))
                .Select(payload => new EmailAttachment
                {
                    FileName = payload.FileName,
                    ContentType = string.IsNullOrWhiteSpace(payload.ContentType) ? "application/octet-stream" : payload.ContentType,
                    Content = string.IsNullOrWhiteSpace(payload.ContentBase64)
                        ? Array.Empty<byte>()
                        : Convert.FromBase64String(payload.ContentBase64)
                })
                .ToArray();
        }
        catch
        {
            return Array.Empty<EmailAttachment>();
        }
    }

    private sealed class EmailAttachmentPayload
    {
        public string FileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = "application/octet-stream";
        public string ContentBase64 { get; set; } = string.Empty;
    }
}
