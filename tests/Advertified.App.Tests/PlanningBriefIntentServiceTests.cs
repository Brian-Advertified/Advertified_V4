using Advertified.App.Configuration;
using Advertified.App.Contracts.Campaigns;
using Advertified.App.Domain.Campaigns;
using Advertified.App.Services;
using Advertified.App.Services.Abstractions;
using FluentAssertions;

namespace Advertified.App.Tests;

public sealed class PlanningBriefIntentServiceTests
{
    [Fact]
    public void FilterEligibleCandidates_WhenLocalOohMissesRichBriefSignals_RejectsCandidate()
    {
        var policyService = CreatePolicyService();
        var briefIntentService = CreateBriefIntentService();
        var service = new PlanningEligibilityService(policyService, new TestBroadcastMasterDataService(), briefIntentService);
        var request = new CampaignPlanningRequest
        {
            SelectedBudget = 120000m,
            GeographyScope = "local",
            Cities = new List<string> { "Johannesburg" },
            TargetLatitude = -26.1076,
            TargetLongitude = 28.0567,
            Objective = "awareness",
            PricePositioning = "premium",
            TargetGender = "female",
            TargetAgeMin = 25,
            TargetAgeMax = 44,
            TargetInterests = new List<string> { "retail", "fashion", "shopping" }
        };

        var strongFit = new InventoryCandidate
        {
            SourceId = Guid.NewGuid(),
            SourceType = "ooh",
            DisplayName = "Sandton Premium Mall Screen",
            MediaType = "OOH",
            City = "Johannesburg",
            Suburb = "Sandton",
            Area = "Sandton",
            Latitude = -26.1078,
            Longitude = 28.0569,
            Cost = 60000m,
            IsAvailable = true,
            Metadata = new Dictionary<string, object?>
            {
                ["pricePositioningFit"] = "premium",
                ["audienceGenderSkew"] = "female shoppers",
                ["audienceAgeSkew"] = "25-44",
                ["venueType"] = "premium_mall",
                ["audienceKeywords"] = new[] { "retail", "fashion", "shopping" }
            }
        };

        var weakFit = new InventoryCandidate
        {
            SourceId = Guid.NewGuid(),
            SourceType = "ooh",
            DisplayName = "Sunnyside Street Pole",
            MediaType = "OOH",
            City = "Johannesburg",
            Suburb = "Sunnyside",
            Area = "Sunnyside",
            Latitude = -26.1080,
            Longitude = 28.0571,
            Cost = 50000m,
            IsAvailable = true,
            Metadata = new Dictionary<string, object?>
            {
                ["pricePositioningFit"] = "mass_market",
                ["audienceGenderSkew"] = "male commuters",
                ["audienceAgeSkew"] = "18-24",
                ["audienceKeywords"] = new[] { "commuter", "street" }
            }
        };

        var result = service.FilterEligibleCandidates(new List<InventoryCandidate> { strongFit, weakFit }, request);

        result.Candidates.Should().ContainSingle(candidate => candidate.DisplayName == "Sandton Premium Mall Screen");
        result.Rejections.Should().Contain(rejection => rejection.Reason == "brief_intent_mismatch" && rejection.DisplayName == "Sunnyside Street Pole");
    }

    [Fact]
    public void AnalyzeCandidate_WhenBriefIntentSignalsAlign_AddsMaterialScoreBonus()
    {
        var policyService = CreatePolicyService();
        var briefIntentService = CreateBriefIntentService();
        var scoreService = new PlanningScoreService(
            policyService,
            new TestBroadcastMasterDataService(),
            new StubLeadMasterDataService(),
            new StubIndustryArchetypeScoringService(),
            briefIntentService);

        var request = new CampaignPlanningRequest
        {
            SelectedBudget = 120000m,
            GeographyScope = "local",
            Cities = new List<string> { "Johannesburg" },
            TargetLatitude = -26.1076,
            TargetLongitude = 28.0567,
            Objective = "awareness",
            PricePositioning = "premium",
            BuyingBehaviour = "impulse",
            TargetGender = "female",
            TargetAgeMin = 25,
            TargetAgeMax = 44,
            TargetLanguages = new List<string> { "English" },
            TargetInterests = new List<string> { "retail", "fashion", "shopping" }
        };

        var alignedCandidate = new InventoryCandidate
        {
            SourceId = Guid.NewGuid(),
            SourceType = "ooh",
            DisplayName = "Sandton Premium Mall Screen",
            MediaType = "OOH",
            City = "Johannesburg",
            Area = "Sandton",
            Latitude = -26.1078,
            Longitude = 28.0569,
            Cost = 60000m,
            IsAvailable = true,
            Language = "English",
            Metadata = new Dictionary<string, object?>
            {
                ["pricePositioningFit"] = "premium",
                ["buyingBehaviourFit"] = "impulse",
                ["audienceGenderSkew"] = "female shoppers",
                ["audienceAgeSkew"] = "25-44",
                ["venueType"] = "premium_mall",
                ["audienceKeywords"] = new[] { "retail", "fashion", "shopping" }
            }
        };

        var weakCandidate = new InventoryCandidate
        {
            SourceId = Guid.NewGuid(),
            SourceType = "ooh",
            DisplayName = "Generic Street Billboard",
            MediaType = "OOH",
            City = "Johannesburg",
            Area = "Johannesburg CBD",
            Latitude = -26.1100,
            Longitude = 28.0600,
            Cost = 60000m,
            IsAvailable = true,
            Language = "English",
            Metadata = new Dictionary<string, object?>
            {
                ["pricePositioningFit"] = "mass_market",
                ["buyingBehaviourFit"] = "planned",
                ["audienceGenderSkew"] = "male commuters",
                ["audienceAgeSkew"] = "18-24",
                ["audienceKeywords"] = new[] { "commuter" }
            }
        };

        var alignedScore = scoreService.AnalyzeCandidate(alignedCandidate, request).Score;
        var weakScore = scoreService.AnalyzeCandidate(weakCandidate, request).Score;

        alignedScore.Should().BeGreaterThan(weakScore + 12m);
        alignedCandidate.Metadata.Should().ContainKey("briefIntentMatchedDimensions");
    }

    private static PlanningPolicyService CreatePolicyService()
    {
        return new PlanningPolicyService(new PlanningPolicySnapshotProvider(new PlanningPolicyOptions
        {
            Scale = new PackagePlanningPolicy
            {
                BudgetFloor = 500000m,
                MinimumNationalRadioCandidates = 1,
                RequireNationalCapableRadio = true,
                RequirePremiumNationalRadio = false,
                NationalRadioBonus = 12,
                NonNationalRadioPenalty = 8,
                RegionalRadioPenalty = 16
            },
            Dominance = new PackagePlanningPolicy
            {
                BudgetFloor = 1000000m,
                MinimumNationalRadioCandidates = 2,
                RequireNationalCapableRadio = true,
                RequirePremiumNationalRadio = true,
                NationalRadioBonus = 18,
                NonNationalRadioPenalty = 12,
                RegionalRadioPenalty = 24
            }
        }));
    }

    private static PlanningBriefIntentService CreateBriefIntentService()
    {
        return new PlanningBriefIntentService(
            new PlanningBriefIntentSettingsSnapshotProvider(new PlanningBriefIntentSettingsSnapshot
            {
                LocalOohMinDimensionMatches = 2,
                LocalOohRadiusKm = 20,
                RelaxedLocalOohRadiusKm = 35,
                ScorePerMatch = 4,
                FullMatchBonus = 4,
                RequireLocalOohAudienceEvidence = true
            }),
            new TestBroadcastMasterDataService());
    }

    private sealed class StubLeadMasterDataService : ILeadMasterDataService
    {
        public LeadMasterTokenSet GetTokenSet() => new();
        public MasterLocationMatch? ResolveLocation(string? value) => null;
        public MasterIndustryMatch? ResolveIndustry(string? value) => null;
        public MasterIndustryMatch? ResolveIndustryFromHints(IReadOnlyList<string> hints) => null;
        public MasterLanguageMatch? ResolveLanguage(string? value) => null;
    }

    private sealed class StubIndustryArchetypeScoringService : IIndustryArchetypeScoringService
    {
        public IndustryArchetypeScoringProfile? Resolve(string? industryCode) => null;
        public IReadOnlyCollection<string> GetSupportedIndustryCodes() => Array.Empty<string>();
    }
}
