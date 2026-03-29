using Advertified.App.Services.Abstractions;
using Advertified.App.Services.BroadcastMatching;
using FluentAssertions;

namespace Advertified.App.Tests;

public sealed class BroadcastMatchingEngineTests
{
    private readonly IBroadcastMatchingEngine _engine;

    public BroadcastMatchingEngineTests()
    {
        var policy = BroadcastMatcherPolicy.Default;
        _engine = new BroadcastMatchingEngine(
            new BroadcastMatchRequestNormalizer(),
            new BroadcastMatchRequestValidator(),
            new BroadcastHardFilterEngine(),
            new BroadcastScoreCalculator(policy),
            new BroadcastRecommendationRanker());
    }

    [Fact]
    public void Match_StrictModeReturnsHealthyPricedCandidate()
    {
        var request = CreateRequest();
        var outlets = new[]
        {
            CreateOutlet(
                name: "Kaya 959",
                provinces: new[] { "gauteng" },
                cities: new[] { "Johannesburg" },
                languages: new[] { "english" },
                keywords: new[] { "urban", "music", "aspirational" },
                pricing: new[] { 25000m, 40000m, 65000m },
                hasPackagePricing: true,
                hasSlotRatePricing: true,
                weekly: 650000,
                daily: 180000,
                catalogHealth: BroadcastCatalogHealth.Strong),
            CreateOutlet(
                name: "Weak No Inventory FM",
                provinces: new[] { "gauteng" },
                pricing: Array.Empty<decimal>(),
                weekly: null,
                daily: null,
                catalogHealth: BroadcastCatalogHealth.WeakNoInventory,
                hasPricing: false)
        };

        var result = _engine.Match(outlets, request);

        result.ModeUsed.Should().Be(BroadcastMatchingMode.StrictFilterThenScore);
        result.Results.Should().ContainSingle();
        result.Results[0].Outlet.Name.Should().Be("Kaya 959");
        result.Results[0].RecommendationTier.Should().Be(BroadcastRecommendationTier.Premium);
        result.ExcludedCounts.Should().ContainKey("weak_no_inventory");
    }

    [Fact]
    public void Match_FallsBackToSoftModeWhenStrictExcludesUnpricedCandidate()
    {
        var request = CreateRequest(
            provinceCodes: new[] { "kwazulu_natal" },
            cityLabels: new[] { "Durban" },
            languages: new[] { "isizulu" },
            keywords: new[] { "music" });
        var outlets = new[]
        {
            CreateOutlet(
                name: "Ukhozi FM",
                provinces: new[] { "kwazulu_natal" },
                cities: new[] { "Durban" },
                languages: new[] { "isizulu" },
                keywords: new[] { "community", "music" },
                pricing: Array.Empty<decimal>(),
                weekly: 500000,
                daily: 120000,
                catalogHealth: BroadcastCatalogHealth.WeakUnpriced,
                hasPricing: false)
        };

        var result = _engine.Match(outlets, request);

        result.ModeUsed.Should().Be(BroadcastMatchingMode.SoftScoreWithPenalties);
        result.Results.Should().ContainSingle();
        result.Results[0].Outlet.Name.Should().Be("Ukhozi FM");
        result.Warnings.Should().ContainSingle(message => message.Contains("strict mode", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Match_UsesSoftModeForNationalFallbackWhenStrictFails()
    {
        var request = CreateRequest(
            provinceCodes: new[] { "gauteng", "western_cape" },
            cityLabels: Array.Empty<string>(),
            requestedCoverageType: BroadcastCoverageType.Regional);

        var outlets = new[]
        {
            CreateOutlet(
                name: "Metro FM",
                provinces: Array.Empty<string>(),
                cities: Array.Empty<string>(),
                isNational: true,
                coverageType: BroadcastCoverageType.National,
                pricing: new[] { 50000m },
                weekly: 900000,
                daily: 220000,
                catalogHealth: BroadcastCatalogHealth.MixedNotFullyHealthy,
                hasPackagePricing: true,
                hasSlotRatePricing: false,
                keywords: Array.Empty<string>(),
                dataSourceEnrichment: "undated source"),
            CreateOutlet(
                name: "Cape Local FM",
                provinces: new[] { "western_cape" },
                cities: new[] { "Cape Town" },
                pricing: Array.Empty<decimal>(),
                weekly: 200000,
                daily: 50000,
                catalogHealth: BroadcastCatalogHealth.WeakUnpriced,
                hasPricing: false)
        };

        var result = _engine.Match(outlets, request);

        result.ModeUsed.Should().Be(BroadcastMatchingMode.SoftScoreWithPenalties);
        result.Results.Should().HaveCount(2);
        result.Results[0].Outlet.Name.Should().Be("Metro FM");
        result.Warnings.Should().ContainSingle(message => message.Contains("strict mode", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Match_RanksStrongHealthyOutletAboveMixedOutletWhenSignalsAreOtherwiseSimilar()
    {
        var request = CreateRequest();
        var strong = CreateOutlet(
            name: "JOZI FM",
            provinces: new[] { "gauteng" },
            cities: new[] { "Johannesburg" },
            languages: new[] { "english" },
            keywords: new[] { "urban", "community" },
            pricing: new[] { 20000m, 30000m },
            weekly: 350000,
            daily: 90000,
            catalogHealth: BroadcastCatalogHealth.Strong,
            hasPackagePricing: true,
            hasSlotRatePricing: true);
        var mixed = CreateOutlet(
            name: "Metro FM",
            provinces: new[] { "gauteng" },
            cities: new[] { "Johannesburg" },
            languages: new[] { "english" },
            keywords: new[] { "urban", "community" },
            pricing: new[] { 22000m },
            weekly: 350000,
            daily: 90000,
            catalogHealth: BroadcastCatalogHealth.MixedNotFullyHealthy,
            hasPackagePricing: true,
            hasSlotRatePricing: false);

        var result = _engine.Match(new[] { mixed, strong }, request);

        result.Results.Should().HaveCount(2);
        result.Results[0].Outlet.Name.Should().Be("JOZI FM");
        result.Results[0].RankPosition.Should().Be(1);
        result.Results[1].RankPosition.Should().Be(2);
    }

    private static BroadcastMatchRequest CreateRequest(
        IEnumerable<string>? provinceCodes = null,
        IEnumerable<string>? cityLabels = null,
        IEnumerable<string>? languages = null,
        IEnumerable<string>? keywords = null,
        BroadcastCoverageType? requestedCoverageType = BroadcastCoverageType.Regional)
    {
        return new BroadcastMatchRequest
        {
            CampaignId = "cmp-001",
            RequestedMediaTypes = new List<BroadcastMediaType> { BroadcastMediaType.Radio },
            TargetProvinceCodes = provinceCodes?.ToList() ?? new List<string> { "gauteng" },
            TargetCityLabels = cityLabels?.ToList() ?? new List<string> { "Johannesburg" },
            TargetLanguages = languages?.ToList() ?? new List<string> { "english" },
            TargetAgeSkew = "25-49",
            TargetGenderSkew = "female-skewed",
            TargetLsmRange = "LSM 7-10",
            TargetUrbanRural = BroadcastUrbanRuralMix.Urban,
            TargetKeywords = keywords?.ToList() ?? new List<string> { "urban", "music" },
            RequestedCoverageType = requestedCoverageType,
            BudgetMinZar = 20000m,
            BudgetMaxZar = 90000m,
            MinWeeklyListenership = 100000
        };
    }

    private static BroadcastMediaOutlet CreateOutlet(
        string name,
        IEnumerable<string>? provinces = null,
        IEnumerable<string>? cities = null,
        IEnumerable<string>? languages = null,
        IEnumerable<string>? keywords = null,
        IEnumerable<decimal>? pricing = null,
        long? weekly = 300000,
        long? daily = 80000,
        BroadcastCatalogHealth catalogHealth = BroadcastCatalogHealth.Strong,
        BroadcastCoverageType coverageType = BroadcastCoverageType.Regional,
        bool isNational = false,
        bool? hasPricing = null,
        bool hasPackagePricing = false,
        bool hasSlotRatePricing = false,
        string dataSourceEnrichment = "current_enrichment")
    {
        var pricePoints = pricing?.ToList() ?? new List<decimal>();
        return new BroadcastMediaOutlet
        {
            Id = Guid.NewGuid(),
            Code = name.ToLowerInvariant().Replace(" ", "-"),
            Name = name,
            MediaType = BroadcastMediaType.Radio,
            CoverageType = coverageType,
            CatalogHealth = catalogHealth,
            IsNational = isNational,
            HasPricing = hasPricing ?? pricePoints.Count > 0,
            PrimaryLanguages = (languages?.Select(NormalizeToken) ?? new[] { "english" }).ToList(),
            ProvinceCodes = (provinces?.Select(NormalizeToken) ?? new[] { "gauteng" }).ToList(),
            CityNames = (cities ?? new[] { "Johannesburg" }).ToList(),
            Keywords = (keywords?.Select(NormalizeToken) ?? new[] { "urban" }).ToList(),
            PricePointsZar = pricePoints,
            HasPackagePricing = hasPackagePricing,
            HasSlotRatePricing = hasSlotRatePricing,
            ListenershipWeekly = weekly,
            ListenershipDaily = daily,
            AudienceAgeSkew = "25-49",
            AudienceGenderSkew = "female-skewed",
            AudienceLsmRange = "LSM 7-10",
            UrbanRuralMix = BroadcastUrbanRuralMix.Urban,
            DataSourceEnrichment = dataSourceEnrichment,
            DataQualityFlags = new List<string>()
        };
    }

    private static string NormalizeToken(string value) =>
        value.Trim().Replace(" ", "_").Replace("-", "_").ToLowerInvariant();
}
