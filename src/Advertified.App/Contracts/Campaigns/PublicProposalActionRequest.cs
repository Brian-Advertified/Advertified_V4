namespace Advertified.App.Contracts.Campaigns;

public sealed class PublicProposalActionRequest
{
    public string Token { get; set; } = string.Empty;
    public Guid? RecommendationId { get; set; }
    public string? Notes { get; set; }
}
