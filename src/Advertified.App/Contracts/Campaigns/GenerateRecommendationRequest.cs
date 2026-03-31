namespace Advertified.App.Contracts.Campaigns;

public sealed class GenerateRecommendationRequest
{
    public int? TargetRadioShare { get; set; }
    public int? TargetOohShare { get; set; }
    public int? TargetTvShare { get; set; }
    public int? TargetDigitalShare { get; set; }
}
