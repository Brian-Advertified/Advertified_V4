namespace Advertified.App.Data.Entities;

public sealed class PackageBandPreviewTier
{
    public Guid Id { get; set; }

    public Guid PackageBandId { get; set; }

    public string TierCode { get; set; } = string.Empty;

    public string TierLabel { get; set; } = string.Empty;

    public string TypicalInclusionsJson { get; set; } = "[]";

    public string IndicativeMixJson { get; set; } = "[]";

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
