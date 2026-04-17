using System;
using System.Collections.Generic;

namespace Advertified.App.Domain.Campaigns;

public sealed class RecommendationRunTrace
{
    public CampaignPlanningRequestSnapshot RequestSnapshot { get; set; } = new();
    public List<RecommendationTraceCount> CandidateCounts { get; set; } = new();
    public List<RecommendationRejectedCandidateTrace> RejectedCandidates { get; set; } = new();
    public List<RecommendationSelectedItemTrace> SelectedItems { get; set; } = new();
}

public sealed class CampaignPlanningRequestSnapshot
{
    public Guid CampaignId { get; set; }
    public decimal SelectedBudget { get; set; }
    public CampaignBusinessLocationSnapshot? BusinessLocation { get; set; }
    public CampaignTargetingProfileSnapshot? Targeting { get; set; }
    public PlanningBudgetAllocationSnapshot? BudgetAllocation { get; set; }
    public string? Objective { get; set; }
    public string? BusinessStage { get; set; }
    public string? MonthlyRevenueBand { get; set; }
    public string? SalesModel { get; set; }
    public string? GeographyScope { get; set; }
    public List<string> Provinces { get; set; } = new();
    public List<string> Cities { get; set; } = new();
    public List<string> Suburbs { get; set; } = new();
    public List<string> Areas { get; set; } = new();
    public string? TargetLocationLabel { get; set; }
    public string? TargetLocationCity { get; set; }
    public string? TargetLocationProvince { get; set; }
    public string? TargetLocationSource { get; set; }
    public string? TargetLocationPrecision { get; set; }
    public List<string> PreferredMediaTypes { get; set; } = new();
    public List<string> ExcludedMediaTypes { get; set; } = new();
    public List<string> TargetLanguages { get; set; } = new();
    public int? TargetAgeMin { get; set; }
    public int? TargetAgeMax { get; set; }
    public string? TargetGender { get; set; }
    public List<string> TargetInterests { get; set; } = new();
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
    public int? TargetLsmMin { get; set; }
    public int? TargetLsmMax { get; set; }
    public List<string> MustHaveAreas { get; set; } = new();
    public List<string> ExcludedAreas { get; set; } = new();
    public bool OpenToUpsell { get; set; }
    public decimal? AdditionalBudget { get; set; }
    public int? MaxMediaItems { get; set; }
    public int? TargetRadioShare { get; set; }
    public int? TargetOohShare { get; set; }
    public int? TargetTvShare { get; set; }
    public int? TargetDigitalShare { get; set; }
    public double? TargetLatitude { get; set; }
    public double? TargetLongitude { get; set; }
}

public sealed class CampaignBusinessLocationSnapshot
{
    public string Label { get; set; } = string.Empty;
    public string? Area { get; set; }
    public string? City { get; set; }
    public string? Province { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string Source { get; set; } = "none";
    public string Precision { get; set; } = "unknown";
    public bool IsResolved { get; set; }
}

public sealed class CampaignTargetingProfileSnapshot
{
    public string Scope { get; set; } = string.Empty;
    public string? Label { get; set; }
    public string? City { get; set; }
    public string? Province { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string Source { get; set; } = "none";
    public string Precision { get; set; } = "unknown";
    public List<string> Provinces { get; set; } = new();
    public List<string> Cities { get; set; } = new();
    public List<string> Suburbs { get; set; } = new();
    public List<string> Areas { get; set; } = new();
    public List<string> PriorityAreas { get; set; } = new();
    public List<string> Exclusions { get; set; } = new();
}

public sealed class PlanningBudgetAllocationSnapshot
{
    public string ChannelPolicyKey { get; set; } = string.Empty;
    public string GeoPolicyKey { get; set; } = string.Empty;
    public string AudienceSegment { get; set; } = string.Empty;
    public List<PlanningChannelAllocationSnapshot> ChannelAllocations { get; set; } = new();
    public List<PlanningGeoAllocationSnapshot> GeoAllocations { get; set; } = new();
    public List<PlanningAllocationLineSnapshot> CompositeAllocations { get; set; } = new();
}

public sealed class PlanningChannelAllocationSnapshot
{
    public string Channel { get; set; } = string.Empty;
    public decimal Weight { get; set; }
    public decimal Amount { get; set; }
}

public sealed class PlanningGeoAllocationSnapshot
{
    public string Bucket { get; set; } = string.Empty;
    public decimal Weight { get; set; }
    public decimal Amount { get; set; }
    public double? RadiusKm { get; set; }
}

public sealed class PlanningAllocationLineSnapshot
{
    public string Channel { get; set; } = string.Empty;
    public string Bucket { get; set; } = string.Empty;
    public decimal Weight { get; set; }
    public decimal Amount { get; set; }
    public double? RadiusKm { get; set; }
}

public sealed class RecommendationTraceCount
{
    public string Stage { get; set; } = string.Empty;
    public string MediaType { get; set; } = string.Empty;
    public int Count { get; set; }
}

public sealed class RecommendationRejectedCandidateTrace
{
    public string Stage { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public Guid SourceId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string MediaType { get; set; } = string.Empty;
}

public sealed class RecommendationSelectedItemTrace
{
    public Guid SourceId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string MediaType { get; set; } = string.Empty;
    public decimal Score { get; set; }
    public decimal TotalCost { get; set; }
    public bool IsUpsell { get; set; }
    public IReadOnlyList<string> SelectionReasons { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> PolicyFlags { get; set; } = Array.Empty<string>();
    public string? ConfidenceScore { get; set; }
}
