namespace Advertified.App.Services.BroadcastMatching;

public enum BroadcastMediaType
{
    Radio,
    Tv
}

public enum BroadcastCoverageType
{
    Local,
    Regional,
    National,
    Mixed,
    Unknown
}

public enum BroadcastCatalogHealth
{
    Strong,
    MixedNotFullyHealthy,
    WeakUnpriced,
    WeakNoInventory,
    WeakPartialPricing,
    Unknown
}

public enum BroadcastUrbanRuralMix
{
    Urban,
    PeriUrban,
    Rural,
    Mixed,
    Unknown
}

public enum BroadcastMatchingMode
{
    StrictFilterThenScore,
    SoftScoreWithPenalties,
    GeographyFirstRescueMode
}

public enum BroadcastRecommendationTier
{
    NotEligible,
    Eligible,
    Recommended,
    Premium
}

public sealed class BroadcastMediaOutlet
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public BroadcastMediaType MediaType { get; set; }
    public BroadcastCoverageType CoverageType { get; set; }
    public BroadcastCatalogHealth CatalogHealth { get; set; }
    public string? OperatorName { get; set; }
    public bool IsNational { get; set; }
    public bool HasPricing { get; set; }
    public string? LanguageNotes { get; set; }
    public string? AudienceAgeSkew { get; set; }
    public string? AudienceGenderSkew { get; set; }
    public string? AudienceLsmRange { get; set; }
    public string? AudienceRacialSkew { get; set; }
    public BroadcastUrbanRuralMix UrbanRuralMix { get; set; } = BroadcastUrbanRuralMix.Unknown;
    public string? BroadcastFrequency { get; set; }
    public long? ListenershipDaily { get; set; }
    public long? ListenershipWeekly { get; set; }
    public string? ListenershipPeriod { get; set; }
    public string? TargetAudience { get; set; }
    public string? DataSourceEnrichment { get; set; }
    public List<string> Keywords { get; set; } = new();
    public List<string> PrimaryLanguages { get; set; } = new();
    public List<string> ProvinceCodes { get; set; } = new();
    public List<string> CityNames { get; set; } = new();
    public List<decimal> PricePointsZar { get; set; } = new();
    public bool HasPackagePricing { get; set; }
    public bool HasSlotRatePricing { get; set; }
    public List<string> DataQualityFlags { get; set; } = new();
}

public sealed class BroadcastMatchRequest
{
    public string CampaignId { get; set; } = string.Empty;
    public List<BroadcastMediaType> RequestedMediaTypes { get; set; } = new();
    public List<string> TargetProvinceCodes { get; set; } = new();
    public List<string> TargetCityLabels { get; set; } = new();
    public List<string> TargetLanguages { get; set; } = new();
    public string? TargetAgeSkew { get; set; }
    public string? TargetGenderSkew { get; set; }
    public string? TargetLsmRange { get; set; }
    public string? TargetRacialSkew { get; set; }
    public BroadcastUrbanRuralMix? TargetUrbanRural { get; set; }
    public List<string> TargetKeywords { get; set; } = new();
    public BroadcastCoverageType? RequestedCoverageType { get; set; }
    public decimal? BudgetMinZar { get; set; }
    public decimal? BudgetMaxZar { get; set; }
    public long? MinWeeklyListenership { get; set; }
    public long? MinDailyListenership { get; set; }
}

public sealed class BroadcastMatchedDimensions
{
    public List<string> Geography { get; set; } = new();
    public List<string> Languages { get; set; } = new();
    public List<string> AudienceKeywords { get; set; } = new();
}

public sealed class BroadcastScoreBreakdown
{
    public decimal GeographyScore { get; set; }
    public decimal AudienceScore { get; set; }
    public decimal LanguageScore { get; set; }
    public decimal PricingScore { get; set; }
    public decimal ListenershipScore { get; set; }
    public decimal MediaTypeScore { get; set; }
    public decimal CoverageScore { get; set; }
    public decimal CatalogHealthScore { get; set; }
    public decimal BoostsScore { get; set; }
    public decimal PenaltiesScore { get; set; }
    public decimal WeightedScore =>
        GeographyScore + AudienceScore + LanguageScore + PricingScore +
        ListenershipScore + MediaTypeScore + CoverageScore + CatalogHealthScore;
}

public sealed class BroadcastMatchCandidate
{
    public BroadcastMediaOutlet Outlet { get; init; } = new();
    public BroadcastScoreBreakdown Breakdown { get; init; } = new();
    public BroadcastMatchedDimensions MatchedDimensions { get; init; } = new();
    public List<string> Flags { get; init; } = new();
    public string ReasoningSummary { get; init; } = string.Empty;
    public decimal FinalScore { get; init; }
    public BroadcastRecommendationTier RecommendationTier { get; init; }
    public int? RankPosition { get; set; }
}

public sealed class BroadcastMatchResponse
{
    public string CampaignId { get; set; } = string.Empty;
    public BroadcastMatchingMode ModeUsed { get; set; }
    public int TotalCandidatesScanned { get; set; }
    public int EligibleCandidates { get; set; }
    public int RecommendedCandidates { get; set; }
    public List<BroadcastMatchCandidate> Results { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public Dictionary<string, int> ExcludedCounts { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
