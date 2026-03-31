namespace Advertified.App.Contracts.Campaigns;

public sealed class CampaignPlanningRequest
{
    public Guid CampaignId { get; set; }
    public decimal SelectedBudget { get; set; }
    public string? GeographyScope { get; set; }
    public List<string> Provinces { get; set; } = new();
    public List<string> Cities { get; set; } = new();
    public List<string> Suburbs { get; set; } = new();
    public List<string> Areas { get; set; } = new();
    public List<string> PreferredMediaTypes { get; set; } = new();
    public List<string> ExcludedMediaTypes { get; set; } = new();
    public List<string> TargetLanguages { get; set; } = new();
    public int? TargetLsmMin { get; set; }
    public int? TargetLsmMax { get; set; }
    public bool OpenToUpsell { get; set; }
    public decimal? AdditionalBudget { get; set; }
    public int? MaxMediaItems { get; set; }
    public int? TargetRadioShare { get; set; }
    public int? TargetOohShare { get; set; }
    public int? TargetTvShare { get; set; }
    public int? TargetDigitalShare { get; set; }
}
