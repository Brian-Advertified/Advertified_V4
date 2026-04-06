using System;
using System.Collections.Generic;

namespace Advertified.App.Data.Entities;

public partial class CampaignBrief
{
    public Guid Id { get; set; }

    public Guid CampaignId { get; set; }

    public string Objective { get; set; } = null!;

    public string? BusinessStage { get; set; }

    public string? MonthlyRevenueBand { get; set; }

    public string? SalesModel { get; set; }

    public DateOnly? StartDate { get; set; }

    public DateOnly? EndDate { get; set; }

    public int? DurationWeeks { get; set; }

    public string GeographyScope { get; set; } = null!;

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

    public string? CustomerType { get; set; }

    public string? BuyingBehaviour { get; set; }

    public string? DecisionCycle { get; set; }

    public string? PricePositioning { get; set; }

    public string? AverageCustomerSpendBand { get; set; }

    public string? GrowthTarget { get; set; }

    public string? UrgencyLevel { get; set; }

    public string? AudienceClarity { get; set; }

    public string? ValuePropositionFocus { get; set; }

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

    public DateTime? SubmittedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual Campaign Campaign { get; set; } = null!;
}
