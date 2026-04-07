namespace Advertified.App.Contracts.Leads;

public sealed class CreateLeadInteractionRequest
{
    public int? LeadActionId { get; init; }

    public string InteractionType { get; init; } = string.Empty;

    public string Notes { get; init; } = string.Empty;
}
