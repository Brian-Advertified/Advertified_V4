namespace Advertified.App.Contracts.Campaigns;

public sealed class CampaignPlanningTargetResponse
{
    public string? Scope { get; set; }
    public string Label { get; set; } = string.Empty;
    public string? Area { get; set; }
    public string? City { get; set; }
    public string? Province { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string Source { get; set; } = "none";
    public string Precision { get; set; } = "unknown";
    public IReadOnlyList<string> PriorityAreas { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> Exclusions { get; set; } = Array.Empty<string>();
}
