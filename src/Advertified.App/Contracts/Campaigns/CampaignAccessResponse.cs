namespace Advertified.App.Contracts.Campaigns;

public sealed record CampaignAccessResponse(
    bool CanOpenBrief,
    bool CanOpenPlanning);
