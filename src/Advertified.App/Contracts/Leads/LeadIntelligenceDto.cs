namespace Advertified.App.Contracts.Leads;

public sealed class LeadIntelligenceDto
{
    public LeadDto Lead { get; init; } = new();

    public SignalDto? LatestSignal { get; init; }

    public LeadScoreDto Score { get; init; } = new();

    public string Insight { get; init; } = string.Empty;

    public string TrendSummary { get; init; } = string.Empty;

    public IReadOnlyList<LeadChannelDetectionDto> ChannelDetections { get; init; } = Array.Empty<LeadChannelDetectionDto>();

    public IReadOnlyList<SignalDto> SignalHistory { get; init; } = Array.Empty<SignalDto>();

    public IReadOnlyList<LeadInsightDto> InsightHistory { get; init; } = Array.Empty<LeadInsightDto>();

    public IReadOnlyList<LeadActionDto> RecommendedActions { get; init; } = Array.Empty<LeadActionDto>();

    public IReadOnlyList<LeadInteractionDto> InteractionHistory { get; init; } = Array.Empty<LeadInteractionDto>();
}
