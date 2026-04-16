namespace Advertified.App.Contracts.Campaigns;

public sealed class SaveCampaignBriefRequest
{
    public string Objective { get; set; } = string.Empty;
    public string? BusinessStage { get; set; }
    public string? MonthlyRevenueBand { get; set; }
    public string? SalesModel { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public int? DurationWeeks { get; set; }
    public string GeographyScope { get; set; } = string.Empty;
    public List<string>? Provinces { get; set; }
    public List<string>? Cities { get; set; }
    public List<string>? Suburbs { get; set; }
    public List<string>? Areas { get; set; }
    public string? TargetLocationLabel { get; set; }
    public string? TargetLocationCity { get; set; }
    public string? TargetLocationProvince { get; set; }
    public double? TargetLatitude { get; set; }
    public double? TargetLongitude { get; set; }
    public int? TargetAgeMin { get; set; }
    public int? TargetAgeMax { get; set; }
    public string? TargetGender { get; set; }
    public List<string>? TargetLanguages { get; set; }
    public int? TargetLsmMin { get; set; }
    public int? TargetLsmMax { get; set; }
    public List<string>? TargetInterests { get; set; }
    public string? TargetAudienceNotes { get; set; }
    public string? CustomerType { get; set; }
    public string? BuyingBehaviour { get; set; }
    public string? DecisionCycle { get; set; }
    public string? PricePositioning { get; set; }
    public string? AverageCustomerSpendBand { get; set; }
    public string? GrowthTarget { get; set; }
    public string? UrgencyLevel { get; set; }
    public string? AudienceClarity { get; set; }
    public string? ValuePropositionFocus { get; set; }
    public List<string>? PreferredMediaTypes { get; set; }
    public List<string>? ExcludedMediaTypes { get; set; }
    public List<string>? MustHaveAreas { get; set; }
    public List<string>? ExcludedAreas { get; set; }
    public bool? CreativeReady { get; set; }
    public string? CreativeNotes { get; set; }
    public int? MaxMediaItems { get; set; }
    public bool OpenToUpsell { get; set; }
    public decimal? AdditionalBudget { get; set; }
    public string? SpecialRequirements { get; set; }
    public string? PreferredVideoAspectRatio { get; set; }
    public int? PreferredVideoDurationSeconds { get; set; }
}
