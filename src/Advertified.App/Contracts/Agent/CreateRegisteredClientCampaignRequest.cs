namespace Advertified.App.Contracts.Agent;

public sealed class CreateRegisteredClientCampaignRequest
{
    public string Email { get; init; } = string.Empty;
    public Guid PackageBandId { get; init; }
    public string? CampaignName { get; init; }
}
