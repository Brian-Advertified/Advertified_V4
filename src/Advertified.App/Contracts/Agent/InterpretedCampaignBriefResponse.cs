namespace Advertified.App.Contracts.Agent;

public sealed class InterpretedCampaignBriefResponse
{
    public string Objective { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public string Scope { get; set; } = string.Empty;
    public string Geography { get; set; } = string.Empty;
    public string Tone { get; set; } = string.Empty;
    public string CampaignName { get; set; } = string.Empty;
    public IReadOnlyList<string> Channels { get; set; } = Array.Empty<string>();
    public string Summary { get; set; } = string.Empty;
}
