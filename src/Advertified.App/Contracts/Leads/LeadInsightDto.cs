namespace Advertified.App.Contracts.Leads;

public sealed class LeadInsightDto
{
    public int Id { get; init; }

    public int LeadId { get; init; }

    public int? SignalId { get; init; }

    public string TrendSummary { get; init; } = string.Empty;

    public int ScoreSnapshot { get; init; }

    public string IntentLevelSnapshot { get; init; } = string.Empty;

    public string Text { get; init; } = string.Empty;

    public DateTime CreatedAt { get; init; }
}
