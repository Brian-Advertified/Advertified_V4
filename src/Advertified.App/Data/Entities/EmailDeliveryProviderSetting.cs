namespace Advertified.App.Data.Entities;

public sealed class EmailDeliveryProviderSetting
{
    public string ProviderKey { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public bool WebhookEnabled { get; set; }
    public string? WebhookSigningSecret { get; set; }
    public string? WebhookEndpointPath { get; set; }
    public string AllowedEventTypesJson { get; set; } = "[]";
    public int MaxSignatureAgeSeconds { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
