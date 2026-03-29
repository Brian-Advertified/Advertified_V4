namespace Advertified.App.Contracts.Agent;

public sealed class InterpretCampaignBriefRequest
{
    public string Brief { get; set; } = string.Empty;
    public string? CampaignName { get; set; }
    public decimal SelectedBudget { get; set; }
}
