namespace Advertified.App.Configuration;

public sealed class AdPlatformOptions
{
    public const string SectionName = "AdPlatforms";

    public bool DryRunMode { get; set; } = true;
    public int MetricsSyncIntervalMinutes { get; set; } = 10;
    public AdPlatformProviderOptions Meta { get; set; } = new();
    public AdPlatformProviderOptions GoogleAds { get; set; } = new();
}

public sealed class AdPlatformProviderOptions
{
    public bool Enabled { get; set; }
    public string ApiKey { get; set; } = string.Empty;
    public string AccountId { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = string.Empty;
    public string PublishPath { get; set; } = string.Empty;
    public string MetricsPath { get; set; } = string.Empty;
    public string RefreshTokenPath { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; } = 30;
}
