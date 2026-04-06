namespace Advertified.App.Contracts.Campaigns;

public sealed class PublicProspectQuestionnaireRequest
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? BusinessName { get; set; }
    public string? Industry { get; set; }
    public Guid PackageBandId { get; set; }
    public string? CampaignName { get; set; }
    public SaveCampaignBriefRequest Brief { get; set; } = new();
}

public sealed class PublicProspectQuestionnaireResponse
{
    public Guid CampaignId { get; set; }
    public string CampaignName { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}
