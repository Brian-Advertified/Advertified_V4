namespace Advertified.App.Services;

public sealed class LeadScoreResult
{
    public int LeadId { get; init; }

    public int Score { get; init; }

    public string IntentLevel { get; init; } = string.Empty;
}
