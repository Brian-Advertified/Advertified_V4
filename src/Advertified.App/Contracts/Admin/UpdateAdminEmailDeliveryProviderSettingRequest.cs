namespace Advertified.App.Contracts.Admin;

public sealed class UpdateAdminEmailDeliveryProviderSettingRequest
{
    public bool WebhookEnabled { get; set; }
    public string? WebhookSigningSecret { get; set; }
    public string? WebhookEndpointPath { get; set; }
    public IReadOnlyList<string>? AllowedEventTypes { get; set; }
    public int MaxSignatureAgeSeconds { get; set; } = 300;
}
