namespace Advertified.App.Contracts.Leads;

public sealed class LeadScoreDto
{
    public int LeadId { get; init; }

    public int Score { get; init; }

    public string IntentLevel { get; init; } = string.Empty;
}
