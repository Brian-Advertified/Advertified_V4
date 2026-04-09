namespace Advertified.App.Services;

public sealed class IndustryArchetypeScoringProfile
{
    public string IndustryCode { get; init; } = string.Empty;

    public decimal MetadataTagMatchScore { get; init; }

    public IReadOnlyDictionary<string, decimal> MediaTypeScores { get; init; } = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, decimal> AudienceHintScores { get; init; } = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
}
