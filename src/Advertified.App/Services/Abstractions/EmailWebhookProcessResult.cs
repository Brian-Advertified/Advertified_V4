namespace Advertified.App.Services.Abstractions;

public sealed class EmailWebhookProcessResult
{
    public bool SignatureValid { get; init; }
    public bool Duplicate { get; init; }
    public string ProcessingStatus { get; init; } = string.Empty;
    public string? ProcessingNotes { get; init; }
    public string? EventType { get; init; }
    public string? WebhookMessageId { get; init; }
}
