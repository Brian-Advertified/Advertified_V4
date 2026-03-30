using Advertified.App.Contracts.Creative;

namespace Advertified.App.Contracts.Campaigns;

public sealed class CampaignCreativeSystemResponse
{
    public Guid Id { get; set; }
    public string Prompt { get; set; } = string.Empty;
    public string? IterationLabel { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public CreativeSystemResponse Output { get; set; } = new();
}
