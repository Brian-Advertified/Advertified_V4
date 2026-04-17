namespace Advertified.App.Services.Abstractions;

public sealed class EmailTrackingContext
{
    public string Purpose { get; init; } = string.Empty;
    public Guid? CampaignId { get; init; }
    public Guid? RecommendationId { get; init; }
    public int? RecommendationRevisionNumber { get; init; }
    public Guid? RecipientUserId { get; init; }
    public Guid? ProspectLeadId { get; init; }
    public IReadOnlyDictionary<string, string?> Metadata { get; init; } = new Dictionary<string, string?>();
}
