using Advertified.App.Data.Entities;

namespace Advertified.App.Services;

public sealed class LeadIntelligenceRunResult
{
    public Lead Lead { get; init; } = null!;

    public Signal Signal { get; init; } = null!;

    public LeadScoreResult Score { get; init; } = new();

    public LeadInsight Insight { get; init; } = null!;

    public IReadOnlyList<LeadAction> RecommendedActions { get; init; } = Array.Empty<LeadAction>();
}
