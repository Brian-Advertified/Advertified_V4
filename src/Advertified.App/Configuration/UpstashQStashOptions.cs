namespace Advertified.App.Configuration;

public sealed class UpstashQStashOptions
{
    public const string SectionName = "UpstashQStash";

    public bool Enabled { get; set; }
    public string BaseUrl { get; set; } = "https://qstash.upstash.io/v2";
    public string Token { get; set; } = string.Empty;
    public string CurrentSigningKey { get; set; } = string.Empty;
    public string NextSigningKey { get; set; } = string.Empty;
    public string VodaPayWebhookJobUrl { get; set; } = string.Empty;
    public int Retries { get; set; } = 3;
}
