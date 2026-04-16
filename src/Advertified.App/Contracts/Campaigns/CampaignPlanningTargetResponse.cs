namespace Advertified.App.Contracts.Campaigns;

public sealed class CampaignPlanningTargetResponse
{
    public string Label { get; set; } = string.Empty;
    public string? City { get; set; }
    public string? Province { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string Source { get; set; } = "none";
    public string Precision { get; set; } = "unknown";
}
