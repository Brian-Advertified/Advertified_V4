namespace Advertified.App.Services.BroadcastMatching;

public sealed class BroadcastMatcherPolicy
{
    public static BroadcastMatcherPolicy Default { get; } = new();

    public decimal MediaTypeWeight { get; init; } = 8m;
    public decimal CoverageWeight { get; init; } = 5m;
    public decimal ProvinceOverlapWeight { get; init; } = 12m;
    public decimal CityOverlapWeight { get; init; } = 8m;
    public decimal NationalBonusWeight { get; init; } = 5m;
    public decimal AgeMatchWeight { get; init; } = 5m;
    public decimal GenderMatchWeight { get; init; } = 2m;
    public decimal LsmMatchWeight { get; init; } = 4m;
    public decimal RacialMatchWeight { get; init; } = 2m;
    public decimal UrbanRuralMatchWeight { get; init; } = 3m;
    public decimal KeywordMatchWeight { get; init; } = 4m;
    public decimal PrimaryLanguageWeight { get; init; } = 8m;
    public decimal LanguageNotesWeight { get; init; } = 4m;
    public decimal BudgetFitWeight { get; init; } = 10m;
    public decimal SlotDensityWeight { get; init; } = 3m;
    public decimal PackagePresenceWeight { get; init; } = 2m;
    public decimal WeeklyListenershipWeight { get; init; } = 6m;
    public decimal DailyListenershipWeight { get; init; } = 4m;
    public decimal StrongCatalogHealthScore { get; init; } = 5m;
    public decimal MixedCatalogHealthScore { get; init; } = 2m;
    public decimal WeakUnpricedCatalogHealthScore { get; init; } = -6m;
    public decimal MissingPricingPenalty { get; init; } = 25m;
    public decimal MissingGeographyPenalty { get; init; } = 12m;
    public decimal MissingAudienceKeywordsPenalty { get; init; } = 5m;
    public decimal MissingListenershipPenalty { get; init; } = 6m;
    public decimal WeakUnpricedPenalty { get; init; } = 10m;
    public decimal MixedHealthPenalty { get; init; } = 4m;
    public decimal StaleEnrichmentPenalty { get; init; } = 3m;
    public decimal RescueCityPenalty { get; init; } = 2m;
    public decimal ProvinceExactBoost { get; init; } = 8m;
    public decimal CityExactBoost { get; init; } = 5m;
    public decimal LanguageExactBoost { get; init; } = 5m;
    public decimal KeywordHighOverlapBoost { get; init; } = 6m;
    public decimal NationalMultiProvinceBoost { get; init; } = 5m;
    public decimal HighListenershipBoost { get; init; } = 4m;
    public decimal DirectPricingBoost { get; init; } = 3m;
    public decimal MaxBoosts { get; init; } = 15m;
    public decimal EligibleThreshold { get; init; } = 55m;
    public decimal RecommendedThreshold { get; init; } = 70m;
    public decimal PremiumThreshold { get; init; } = 82m;
}
