namespace Advertified.App.Contracts.Packages;

public sealed class PackageBandDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal MinBudget { get; set; }
    public decimal MaxBudget { get; set; }
    public int SortOrder { get; set; }
    public string Description { get; set; } = string.Empty;
    public string AudienceFit { get; set; } = string.Empty;
    public string QuickBenefit { get; set; } = string.Empty;
    public string PackagePurpose { get; set; } = string.Empty;
    public string IncludeRadio { get; set; } = "optional";
    public string IncludeTv { get; set; } = "no";
    public string LeadTime { get; set; } = string.Empty;
    public decimal? RecommendedSpend { get; set; }
    public bool IsRecommended { get; set; }
    public List<string> Benefits { get; set; } = new();
    public int MaxAdVariants { get; set; }
    public List<string> AllowedAdPlatforms { get; set; } = new();
    public bool AllowAdMetricsSync { get; set; }
    public bool AllowAdAutoOptimize { get; set; }
    public List<string> AllowedVoicePackTiers { get; set; } = new();
    public int MaxAdRegenerations { get; set; }
}
