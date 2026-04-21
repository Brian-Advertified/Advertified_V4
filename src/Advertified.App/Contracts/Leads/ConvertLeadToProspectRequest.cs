namespace Advertified.App.Contracts.Leads;

public sealed class ConvertLeadToProspectRequest
{
    public string? FullName { get; init; }

    public string? Email { get; init; }

    public string? Phone { get; init; }

    public string QualificationReason { get; init; } = "agent_decision";

    public string? LastOutcome { get; init; }

    public DateTime? NextFollowUpAtUtc { get; init; }

    public Guid? PackageBandId { get; init; }

    public string? CampaignName { get; init; }
}
