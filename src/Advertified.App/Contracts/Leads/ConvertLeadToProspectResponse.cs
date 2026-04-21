namespace Advertified.App.Contracts.Leads;

public sealed class ConvertLeadToProspectResponse
{
    public Guid ProspectLeadId { get; init; }

    public Guid OwnerAgentUserId { get; init; }

    public Guid? CampaignId { get; init; }

    public string UnifiedStatus { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;
}
