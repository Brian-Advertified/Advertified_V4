namespace Advertified.App.Contracts.Campaigns;

public sealed class CampaignRecommendationResponse
{
    public Guid Id { get; set; }
    public Guid CampaignId { get; set; }
    public string Summary { get; set; } = string.Empty;
    public string Rationale { get; set; } = string.Empty;
    public string? ClientFeedbackNotes { get; set; }
    public string Status { get; set; } = string.Empty;
    public decimal TotalCost { get; set; }
    public IReadOnlyList<RecommendationItemResponse> Items { get; set; } = Array.Empty<RecommendationItemResponse>();
}
