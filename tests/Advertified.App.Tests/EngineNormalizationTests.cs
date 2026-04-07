using Advertified.App.Configuration;
using Advertified.App.Contracts.Campaigns;
using Advertified.App.Domain.Campaigns;
using Advertified.App.Services;
using Advertified.App.Services.BroadcastMatching;

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
