namespace Advertified.App.Domain.Campaigns;

public sealed class PlanningBriefIntentSettingsSnapshot
{
    public int LocalOohMinDimensionMatches { get; init; } = 2;
    public double LocalOohRadiusKm { get; init; } = 20d;
    public double RelaxedLocalOohRadiusKm { get; init; } = 35d;
    public decimal ScorePerMatch { get; init; } = 4m;
    public decimal FullMatchBonus { get; init; } = 4m;
    public bool RequireLocalOohAudienceEvidence { get; init; } = true;
}

public sealed class PlanningBriefIntentEvaluation
{
    public int ConsideredDimensionCount { get; init; }
    public int MatchedDimensionCount { get; init; }
    public int RequiredDimensionCount { get; init; }
    public bool IsEligible { get; init; } = true;
    public bool AudienceEvidencePresent { get; init; } = true;
    public decimal ScoreBonus { get; init; }
    public double? DistanceKm { get; init; }
    public IReadOnlyList<string> MatchedDimensions { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> MissingDimensions { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> PolicyFlags { get; init; } = Array.Empty<string>();
}
