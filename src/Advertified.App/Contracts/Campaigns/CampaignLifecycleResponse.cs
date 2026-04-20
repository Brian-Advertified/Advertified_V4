namespace Advertified.App.Contracts.Campaigns;

public sealed class CampaignLifecycleResponse
{
    public string CurrentState { get; set; } = string.Empty;
    public string ProposalState { get; set; } = string.Empty;
    public string PaymentState { get; set; } = string.Empty;
    public string CommercialState { get; set; } = string.Empty;
    public string CommunicationState { get; set; } = string.Empty;
    public string FulfilmentState { get; set; } = string.Empty;
    public string AiStudioAccessState { get; set; } = string.Empty;
}
