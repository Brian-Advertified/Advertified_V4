namespace Advertified.App.Contracts.Campaigns;

public sealed class CampaignChannelFlightRequest
{
    public string Channel { get; set; } = string.Empty;
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public int? DurationWeeks { get; set; }
    public int? DurationMonths { get; set; }
    public int? Priority { get; set; }
    public string? Notes { get; set; }
}
