namespace Advertified.App.Services;

public sealed class LeadChannelDetectionResult
{
    public int LeadId { get; init; }

    public string Channel { get; init; } = string.Empty;

    public int Score { get; init; }

    public string Confidence { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public string DominantReason { get; init; } = string.Empty;

    public DateTime? LastEvidenceAtUtc { get; init; }

    public IReadOnlyList<LeadChannelSignalEvidence> Signals { get; init; } = Array.Empty<LeadChannelSignalEvidence>();
}
