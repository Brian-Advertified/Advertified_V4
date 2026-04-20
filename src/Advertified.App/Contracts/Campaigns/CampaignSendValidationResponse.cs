namespace Advertified.App.Contracts.Campaigns;

public sealed class CampaignSendValidationResponse
{
    public bool CanSendRecommendation { get; set; }
    public IReadOnlyList<string> Reasons { get; set; } = Array.Empty<string>();
}
