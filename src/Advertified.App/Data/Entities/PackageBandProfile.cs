namespace Advertified.App.Data.Entities;

public sealed class PackageBandProfile
{
    public Guid PackageBandId { get; set; }

    public string Description { get; set; } = string.Empty;

    public string AudienceFit { get; set; } = string.Empty;

    public string QuickBenefit { get; set; } = string.Empty;

    public string PackagePurpose { get; set; } = string.Empty;

    public string IncludeRadio { get; set; } = "optional";

    public string IncludeTv { get; set; } = "no";

    public string LeadTimeLabel { get; set; } = string.Empty;

    public decimal? RecommendedSpend { get; set; }

    public bool IsRecommended { get; set; }

    public string BenefitsJson { get; set; } = "[]";

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
