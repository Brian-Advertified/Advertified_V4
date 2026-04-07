namespace Advertified.App.Contracts.Leads;

public sealed class LeadChannelDetectionDto
{
    public int LeadId { get; init; }

    public string Channel { get; init; } = string.Empty;

    public int Score { get; init; }

    public string Confidence { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public string DominantReason { get; init; } = string.Empty;

    public DateTime? LastEvidenceAtUtc { get; init; }

    public IReadOnlyList<LeadChannelSignalDto> Signals { get; init; } = Array.Empty<LeadChannelSignalDto>();
}
