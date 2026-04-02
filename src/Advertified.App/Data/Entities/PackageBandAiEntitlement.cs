namespace Advertified.App.Data.Entities;

public sealed class PackageBandAiEntitlement
{
    public Guid PackageBandId { get; set; }
    public int MaxAdVariants { get; set; }
    public string AllowedAdPlatformsJson { get; set; } = "[]";
    public bool AllowAdMetricsSync { get; set; }
    public bool AllowAdAutoOptimize { get; set; }
    public string AllowedVoicePackTiersJson { get; set; } = "[]";
    public int MaxAdRegenerations { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
