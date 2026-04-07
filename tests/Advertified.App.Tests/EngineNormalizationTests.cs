using Advertified.App.Configuration;
using Advertified.App.Contracts.Campaigns;
using Advertified.App.Domain.Campaigns;
using Advertified.App.Services;
using Advertified.App.Services.BroadcastMatching;
using FluentAssertions;

public class EngineNormalizationTests
{
    [Fact]
    public void PlanningScoreService_AudienceScore_MatchesLanguageAliases()
    {
        var service = CreatePlanningScoreService();
        var candidate = new InventoryCandidate
        {
            MediaType = "Radio",
            Language = "Zulu",
            Metadata = new Dictionary<string, object?>
            {
                ["primaryLanguages"] = new[] { "Zulu" }
            }
        };
        var request = new CampaignPlanningRequest
        {
            TargetLanguages = new List<string> { "isiZulu" }
        };

        var score = service.AudienceScore(candidate, request);

        score.Should().BeGreaterThan(0m);
    }

    [Theory]
    [InlineData("all")]
    [InlineData("mixed")]
    [InlineData("everyone")]
    public void PlanningScoreService_AudienceScore_TreatsBroadGenderAsNoPreference(string targetGender)
    {
        var service = CreatePlanningScoreService();
        var candidate = new InventoryCandidate
        {
            MediaType = "Radio",
            Metadata = new Dictionary<string, object?>
            {
                ["audienceGenderSkew"] = "female"
            }
        };
        var request = new CampaignPlanningRequest
        {
            TargetGender = targetGender
        };

        var score = service.AudienceScore(candidate, request);

        score.Should().Be(0m);
    }

    [Fact]
    public void PlanningScoreService_AnalyzeCandidate_PrioritizesBroadcastLanguageMatchOverMismatch()
    {
        var service = CreatePlanningScoreService();
        var request = new CampaignPlanningRequest
        {
            GeographyScope = "local",
            Cities = new List<string> { "Johannesburg" },
            PreferredMediaTypes = new List<string> { "radio" },
            TargetLanguages = new List<string> { "isiZulu" },
            SelectedBudget = 100000m,
            Objective = "launch",
            TargetRadioShare = 100
        };

        var matchingCandidate = new InventoryCandidate
        {
            SourceId = Guid.NewGuid(),
            MediaType = "Radio",
            DisplayName = "Ukhozi FM",
            Language = "isiZulu",
            City = "Johannesburg",
            Cost = 10000m,
            IsAvailable = true,
            MarketScope = "local",
            Metadata = new Dictionary<string, object?>
            {
                ["primaryLanguages"] = new[] { "isiZulu" },
                ["cityLabels"] = new[] { "Johannesburg" }
            }
        };

        var mismatchingCandidate = new InventoryCandidate
        {
            SourceId = Guid.NewGuid(),
            MediaType = "Radio",
            DisplayName = "5FM",
            Language = "English",
            City = "Johannesburg",
            Cost = 10000m,
            IsAvailable = true,
            MarketScope = "local",
            Metadata = new Dictionary<string, object?>
            {
                ["primaryLanguages"] = new[] { "English" },
                ["cityLabels"] = new[] { "Johannesburg" }
            }
        };

        var matchingScore = service.AnalyzeCandidate(matchingCandidate, request).Score;
        var mismatchingScore = service.AnalyzeCandidate(mismatchingCandidate, request).Score;

        matchingScore.Should().BeGreaterThan(mismatchingScore);
        matchingScore.Should().BeGreaterThan(mismatchingScore + 40m);
    }

    [Fact]
    public void BroadcastScoreCalculator_Score_MatchesLanguageAliases()
    {
        var calculator = new BroadcastScoreCalculator(BroadcastMatcherPolicy.Default);
        var outlet = new BroadcastMediaOutlet
        {
            Id = Guid.NewGuid(),
            Name = "Zulu Radio",
            MediaType = BroadcastMediaType.Radio,
            CoverageType = BroadcastCoverageType.Regional,
            CatalogHealth = BroadcastCatalogHealth.Strong,
            PrimaryLanguages = new List<string> { "zulu" },
            ProvinceCodes = new List<string> { "gauteng" },
            CityNames = new List<string> { "Johannesburg" },
            Keywords = new List<string> { "retail" },
            HasPricing = true,
            PricePointsZar = new List<decimal> { 1000m },
            HasPackagePricing = true,
            HasSlotRatePricing = true
        };
        var request = new BroadcastMatchRequest
        {
            RequestedMediaTypes = new List<BroadcastMediaType> { BroadcastMediaType.Radio },
            TargetLanguages = new List<string> { "isiZulu" }
        };

        var result = calculator.Score(outlet, request, new[] { outlet }, BroadcastMatchingMode.StrictFilterThenScore);

        result.Breakdown.LanguageScore.Should().BeGreaterThan(0m);
        result.MatchedDimensions.Languages.Should().Contain("zulu");
    }

    private static PlanningScoreService CreatePlanningScoreService()
    {
        var policyService = new PlanningPolicyService(new PlanningPolicySnapshotProvider(new PlanningPolicyOptions
        {
            Scale = new PackagePlanningPolicy(),
            Dominance = new PackagePlanningPolicy()
        }));

        return new PlanningScoreService(policyService);
    }
}
