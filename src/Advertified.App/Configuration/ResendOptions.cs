namespace Advertified.App.Configuration;

public sealed class ResendOptions
{
    public const string SectionName = "Resend";

    public string ApiKey { get; set; } = string.Empty;

    public string BaseUrl { get; set; } = "https://api.resend.com";

    public string FromEmail { get; set; } = string.Empty;

    public string LocalArchiveDirectory { get; set; } = "App_Data/email_outbox";

    public Dictionary<string, string> SenderAddresses { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
