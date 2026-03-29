namespace Advertified.App.Contracts.Campaigns;

public sealed class CampaignRecommendationResponse
{
    public Guid Id { get; set; }
    public Guid CampaignId { get; set; }
    public string ProposalLabel { get; set; } = string.Empty;
    public string ProposalStrategy { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string Rationale { get; set; } = string.Empty;
    public string? ClientFeedbackNotes { get; set; }
    public bool ManualReviewRequired { get; set; }
    public IReadOnlyList<string> FallbackFlags { get; set; } = Array.Empty<string>();
    public string Status { get; set; } = string.Empty;
    public decimal TotalCost { get; set; }
    public IReadOnlyList<RecommendationItemResponse> Items { get; set; } = Array.Empty<RecommendationItemResponse>();
}
