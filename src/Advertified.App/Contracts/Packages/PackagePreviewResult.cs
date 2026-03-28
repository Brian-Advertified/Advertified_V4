namespace Advertified.App.Contracts.Packages;

public sealed class PackagePreviewResult
{
    public decimal Budget { get; set; }

    public string SelectedArea { get; set; } = string.Empty;

    public string TierLabel { get; set; } = string.Empty;

    public string PackagePurpose { get; set; } = string.Empty;

    public decimal? RecommendedSpend { get; set; }

    public string ReachEstimate { get; set; } = string.Empty;

    public string Coverage { get; set; } = string.Empty;

    public List<string> ExampleLocations { get; set; } = new();

    public List<string> RadioSupportExamples { get; set; } = new();

    public List<string> TvSupportExamples { get; set; } = new();

    public List<string> TypicalInclusions { get; set; } = new();

    public List<string> IndicativeMix { get; set; } = new();

    public List<string> MediaMix { get; set; } = new();

    public string Note { get; set; } = string.Empty;
}
