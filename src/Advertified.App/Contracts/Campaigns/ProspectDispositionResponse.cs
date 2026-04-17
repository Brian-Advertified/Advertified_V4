namespace Advertified.App.Contracts.Campaigns;

public sealed class ProspectDispositionResponse
{
    public string Status { get; set; } = string.Empty;
    public string? ReasonCode { get; set; }
    public string? Notes { get; set; }
    public DateTimeOffset? ClosedAt { get; set; }
    public Guid? ClosedByUserId { get; set; }
    public string? ClosedByName { get; set; }
}

public sealed class CloseProspectCampaignRequest
{
    public string ReasonCode { get; set; } = string.Empty;
    public string? Notes { get; set; }
}
