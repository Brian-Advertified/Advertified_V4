namespace Advertified.App.AIPlatform.Api;

public sealed class CreativeQaResultResponse
{
    public Guid CreativeId { get; set; }
    public Guid CampaignId { get; set; }
    public string Channel { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public decimal Clarity { get; set; }
    public decimal Attention { get; set; }
    public decimal EmotionalImpact { get; set; }
    public decimal CtaStrength { get; set; }
    public decimal BrandFit { get; set; }
    public decimal ChannelFit { get; set; }
    public decimal FinalScore { get; set; }
    public string Status { get; set; } = string.Empty;
    public string RiskLevel { get; set; } = string.Empty;
    public IReadOnlyList<string> Issues { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> Suggestions { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> RiskFlags { get; set; } = Array.Empty<string>();
    public string? ImprovedPayloadJson { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
