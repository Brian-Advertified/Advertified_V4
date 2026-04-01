namespace Advertified.App.Domain.Campaigns;

public sealed class CampaignBrief
{
    public Guid Id { get; set; }
    public Guid CampaignId { get; set; }
    public string Objective { get; set; } = string.Empty;
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public int? DurationWeeks { get; set; }
    public string GeographyScope { get; set; } = string.Empty;
    public string? ProvincesJson { get; set; }
    public string? CitiesJson { get; set; }
    public string? SuburbsJson { get; set; }
    public string? AreasJson { get; set; }
    public int? TargetAgeMin { get; set; }
    public int? TargetAgeMax { get; set; }
    public string? TargetGender { get; set; }
    public string? TargetLanguagesJson { get; set; }
    public int? TargetLsmMin { get; set; }
    public int? TargetLsmMax { get; set; }
    public string? TargetInterestsJson { get; set; }
    public string? TargetAudienceNotes { get; set; }
    public string? PreferredMediaTypesJson { get; set; }
    public string? ExcludedMediaTypesJson { get; set; }
    public string? MustHaveAreasJson { get; set; }
    public string? ExcludedAreasJson { get; set; }
    public bool? CreativeReady { get; set; }
    public string? CreativeNotes { get; set; }
    public int? MaxMediaItems { get; set; }
    public bool OpenToUpsell { get; set; }
    public decimal? AdditionalBudget { get; set; }
    public string? SpecialRequirements { get; set; }
    public string? PreferredVideoAspectRatio { get; set; }
    public int? PreferredVideoDurationSeconds { get; set; }
    public DateTimeOffset? SubmittedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
