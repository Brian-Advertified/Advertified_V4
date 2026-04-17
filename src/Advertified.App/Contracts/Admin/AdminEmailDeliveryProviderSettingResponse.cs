namespace Advertified.App.Contracts.Admin;

public sealed class AdminEmailDeliveryProviderSettingResponse
{
    public string ProviderKey { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public bool WebhookEnabled { get; set; }
    public bool HasWebhookSigningSecret { get; set; }
    public string? WebhookEndpointPath { get; set; }
    public IReadOnlyList<string> AllowedEventTypes { get; set; } = Array.Empty<string>();
    public int MaxSignatureAgeSeconds { get; set; }
    public DateTime UpdatedAt { get; set; }
}
