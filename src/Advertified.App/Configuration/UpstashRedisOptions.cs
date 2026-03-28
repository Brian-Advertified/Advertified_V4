namespace Advertified.App.Configuration;

public sealed class UpstashRedisOptions
{
    public const string SectionName = "UpstashRedis";

    public bool Enabled { get; set; }
    public string RestUrl { get; set; } = string.Empty;
    public string RestToken { get; set; } = string.Empty;
    public int DefaultTtlSeconds { get; set; } = 86400;
}
