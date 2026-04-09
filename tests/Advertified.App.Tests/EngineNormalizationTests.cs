using Advertified.App.Configuration;
using Advertified.App.Contracts.Campaigns;
using Advertified.App.Domain.Campaigns;
using Advertified.App.Services;
using Advertified.App.Services.BroadcastMatching;
using Advertified.App.Services.Abstractions;
using Advertified.App.Support;
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

    [Fact]
    public void PlanningScoreService_AudienceScore_MatchesSecondaryLanguageMetadata()
    {
        var service = CreatePlanningScoreService();
        var candidate = new InventoryCandidate
        {
            MediaType = "Radio",
            Metadata = new Dictionary<string, object?>
            {
                ["secondary_language"] = "Afrikaans"
            }
        };
        var request = new CampaignPlanningRequest
        {
            TargetLanguages = new List<string> { "Afrikaans" }
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
    public void PlanningScoreService_AnalyzeCandidate_PrioritizesMetroFmOverChannelAfrica_ForLocalAwareness()
    {
        var service = CreatePlanningScoreService();
        var request = new CampaignPlanningRequest
        {
            GeographyScope = "local",
            Cities = new List<string> { "Johannesburg" },
            PreferredMediaTypes = new List<string> { "radio" },
            SelectedBudget = 100000m,
            Objective = "awareness",
            TargetRadioShare = 100
        };

        var metroCandidate = new InventoryCandidate
        {
            SourceId = Guid.NewGuid(),
            MediaType = "Radio",
            DisplayName = "Metro FM - Breakfast",
            City = "Johannesburg",
            Cost = 12000m,
            IsAvailable = true,
            MarketScope = "national",
            MonthlyListenership = 8_000_000
        };

        var channelAfricaCandidate = new InventoryCandidate
        {
            SourceId = Guid.NewGuid(),
            MediaType = "Radio",
            DisplayName = "Channel Africa - Drive",
            City = "Johannesburg",
            Cost = 12000m,
            IsAvailable = true,
            MarketScope = "national",
            MonthlyListenership = 500_000,
            Metadata = new Dictionary<string, object?>
            {
                ["targetAudience"] = "Pan-African international audience"
            }
        };

        var metroScore = service.AnalyzeCandidate(metroCandidate, request).Score;
        var channelAfricaScore = service.AnalyzeCandidate(channelAfricaCandidate, request).Score;

        metroScore.Should().BeGreaterThan(channelAfricaScore);
    }

    [Fact]
    public void PlanningScoreService_IndustryContextFitScore_FavorsFuneralFriendlyRadio()
    {
        var service = CreatePlanningScoreService();
        var request = new CampaignPlanningRequest
        {
            TargetInterests = new List<string> { "Funeral services" },
            TargetAudienceNotes = "Family decision makers in Johannesburg"
        };

        var trustRadioCandidate = new InventoryCandidate
        {
            MediaType = "Radio",
            DisplayName = "Talk station morning show",
            Metadata = new Dictionary<string, object?>
            {
                ["industry_fit_tags"] = new[] { "funeral_services" },
                ["target_audience"] = "News, trust-focused family audience"
            }
        };

        var genericDigitalCandidate = new InventoryCandidate
        {
            MediaType = "Digital",
            DisplayName = "Generic digital package",
            Metadata = new Dictionary<string, object?>
            {
                ["target_audience"] = "General discovery audience"
            }
        };

        var trustRadioScore = service.IndustryContextFitScore(trustRadioCandidate, request);
        var genericDigitalScore = service.IndustryContextFitScore(genericDigitalCandidate, request);

        trustRadioScore.Should().BeGreaterThan(genericDigitalScore);
    }

    [Fact]
    public void PlanningScoreService_IndustryContextFitScore_ReturnsZeroWhenNoIndustrySignalsProvided()
    {
        var service = CreatePlanningScoreService();
        var request = new CampaignPlanningRequest();
        var candidate = new InventoryCandidate
        {
            MediaType = "Radio",
            DisplayName = "Any station",
            Metadata = new Dictionary<string, object?>()
        };

        var score = service.IndustryContextFitScore(candidate, request);

        score.Should().Be(0m);
    }

    [Fact]
    public void PlanningScoreService_IndustryContextFitScore_UsesAudienceAndGenderRequestFit()
    {
        var service = CreatePlanningScoreService();
        var request = new CampaignPlanningRequest
        {
            TargetInterests = new List<string> { "Funeral services" },
            TargetAudienceNotes = "family trust and older audience",
            TargetGender = "female"
        };

        var alignedCandidate = new InventoryCandidate
        {
            MediaType = "Radio",
            Metadata = new Dictionary<string, object?>
            {
                ["target_audience"] = "older family trust audience",
                ["audience_gender_skew"] = "female-skewed",
                ["industry_fit_tags"] = new[] { "funeral_services" }
            }
        };

        var oppositeGenderCandidate = new InventoryCandidate
        {
            MediaType = "Radio",
            Metadata = new Dictionary<string, object?>
            {
                ["target_audience"] = "older family trust audience",
                ["audience_gender_skew"] = "male-skewed",
                ["industry_fit_tags"] = new[] { "funeral_services" }
            }
        };

        var alignedScore = service.IndustryContextFitScore(alignedCandidate, request);
        var oppositeScore = service.IndustryContextFitScore(oppositeGenderCandidate, request);

        alignedScore.Should().BeGreaterThan(oppositeScore);
    }

    [Fact]
    public void PlanningScoreService_IndustryContextFitScore_Funeral_PrefersRadioTrustInventory()
    {
        var service = CreatePlanningScoreService();
        var request = new CampaignPlanningRequest
        {
            TargetInterests = new List<string> { "Funeral services" },
            TargetAudienceNotes = "family trust and older audience"
        };

        var radioCandidate = new InventoryCandidate
        {
            MediaType = "Radio",
            Metadata = new Dictionary<string, object?>
            {
                ["target_audience"] = "news trust family older audience",
                ["industry_fit_tags"] = new[] { "funeral_services" }
            }
        };
        var digitalCandidate = new InventoryCandidate
        {
            MediaType = "Digital",
            Metadata = new Dictionary<string, object?>
            {
                ["target_audience"] = "general audience"
            }
        };

        var radioScore = service.IndustryContextFitScore(radioCandidate, request);
        var digitalScore = service.IndustryContextFitScore(digitalCandidate, request);

        radioScore.Should().BeGreaterThan(digitalScore);
    }

    [Fact]
    public void PlanningScoreService_IndustryContextFitScore_Restaurant_PrefersDigitalCommuterInventory()
    {
        var service = CreatePlanningScoreService();
        var request = new CampaignPlanningRequest
        {
            TargetInterests = new List<string> { "Restaurant and takeaway" },
            TargetAudienceNotes = "commuter impulse shopping lifestyle audience"
        };

        var digitalCandidate = new InventoryCandidate
        {
            MediaType = "Digital",
            Metadata = new Dictionary<string, object?>
            {
                ["target_audience"] = "commuter impulse lifestyle audience",
                ["industry_fit_tags"] = new[] { "food_hospitality" }
            }
        };
        var radioCandidate = new InventoryCandidate
        {
            MediaType = "Radio",
            Metadata = new Dictionary<string, object?>
            {
                ["target_audience"] = "general adult audience"
            }
        };

        var digitalScore = service.IndustryContextFitScore(digitalCandidate, request);
        var radioScore = service.IndustryContextFitScore(radioCandidate, request);

        digitalScore.Should().BeGreaterThan(radioScore);
    }

    [Fact]
    public void PlanningScoreService_IndustryContextFitScore_Retail_PrefersOohAndRadioOverTv()
    {
        var service = CreatePlanningScoreService();
        var request = new CampaignPlanningRequest
        {
            TargetInterests = new List<string> { "Retail grocery promotions" },
            TargetAudienceNotes = "shopping mass market family audience"
        };

        var oohCandidate = new InventoryCandidate
        {
            MediaType = "OOH",
            Metadata = new Dictionary<string, object?>
            {
                ["target_audience"] = "shopping family mass market",
                ["industry_fit_tags"] = new[] { "retail" }
            }
        };
        var tvCandidate = new InventoryCandidate
        {
            MediaType = "TV",
            Metadata = new Dictionary<string, object?>
            {
                ["target_audience"] = "broad entertainment audience"
            }
        };

        var oohScore = service.IndustryContextFitScore(oohCandidate, request);
        var tvScore = service.IndustryContextFitScore(tvCandidate, request);

        oohScore.Should().BeGreaterThan(tvScore);
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

        return new PlanningScoreService(
            policyService,
            new TestBroadcastMasterDataService(),
            new TestLeadMasterDataService(),
            new TestIndustryArchetypeScoringService());
    }

    private sealed class TestLeadMasterDataService : ILeadMasterDataService
    {
        public LeadMasterTokenSet GetTokenSet() => new();

        public MasterLocationMatch? ResolveLocation(string? value) => null;

        public MasterIndustryMatch? ResolveIndustry(string? value)
        {
            return ResolveIndustryFromHints(string.IsNullOrWhiteSpace(value) ? Array.Empty<string>() : new[] { value });
        }

        public MasterIndustryMatch? ResolveIndustryFromHints(IReadOnlyList<string> hints)
        {
            if (hints.Any(hint => hint.Contains("funeral", StringComparison.OrdinalIgnoreCase)))
            {
                return new MasterIndustryMatch { Code = LeadCanonicalValues.IndustryCodes.FuneralServices, Label = "Funeral Services" };
            }

            if (hints.Any(hint => hint.Contains("retail", StringComparison.OrdinalIgnoreCase)))
            {
                return new MasterIndustryMatch { Code = LeadCanonicalValues.IndustryCodes.Retail, Label = "Retail" };
            }

            if (hints.Any(hint => hint.Contains("restaurant", StringComparison.OrdinalIgnoreCase)
                                  || hint.Contains("takeaway", StringComparison.OrdinalIgnoreCase)
                                  || hint.Contains("food", StringComparison.OrdinalIgnoreCase)))
            {
                return new MasterIndustryMatch { Code = LeadCanonicalValues.IndustryCodes.FoodHospitality, Label = "Food & Hospitality" };
            }

            return null;
        }

        public MasterLanguageMatch? ResolveLanguage(string? value) => null;
    }

    private sealed class TestIndustryArchetypeScoringService : IIndustryArchetypeScoringService
    {
        private readonly IReadOnlyDictionary<string, IndustryArchetypeScoringProfile> _profiles =
            new Dictionary<string, IndustryArchetypeScoringProfile>(StringComparer.OrdinalIgnoreCase)
            {
                [LeadCanonicalValues.IndustryCodes.FuneralServices] = new()
                {
                    IndustryCode = LeadCanonicalValues.IndustryCodes.FuneralServices,
                    MetadataTagMatchScore = 4m,
                    MediaTypeScores = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["radio"] = 6m,
                        ["ooh"] = 4m,
                        ["digital"] = 1m,
                        ["tv"] = 0m
                    },
                    AudienceHintScores = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["news"] = 3m,
                        ["current affairs"] = 3m,
                        ["trust"] = 3m,
                        ["family"] = 3m,
                        ["older"] = 3m
                    }
                },
                [LeadCanonicalValues.IndustryCodes.FoodHospitality] = new()
                {
                    IndustryCode = LeadCanonicalValues.IndustryCodes.FoodHospitality,
                    MetadataTagMatchScore = 4m,
                    MediaTypeScores = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["digital"] = 6m,
                        ["ooh"] = 5m,
                        ["radio"] = 3m,
                        ["tv"] = 0m
                    },
                    AudienceHintScores = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["commuter"] = 3m,
                        ["impulse"] = 3m,
                        ["shopping"] = 3m,
                        ["lifestyle"] = 3m
                    }
                },
                [LeadCanonicalValues.IndustryCodes.Retail] = new()
                {
                    IndustryCode = LeadCanonicalValues.IndustryCodes.Retail,
                    MetadataTagMatchScore = 4m,
                    MediaTypeScores = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["radio"] = 5m,
                        ["ooh"] = 5m,
                        ["digital"] = 4m,
                        ["tv"] = 1m
                    },
                    AudienceHintScores = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["shopping"] = 3m,
                        ["retail"] = 3m,
                        ["mass market"] = 3m,
                        ["family"] = 3m
                    }
                }
            };

        public IndustryArchetypeScoringProfile? Resolve(string? industryCode)
        {
            if (string.IsNullOrWhiteSpace(industryCode))
            {
                return null;
            }

            _profiles.TryGetValue(industryCode, out var profile);
            return profile;
        }

        public IReadOnlyCollection<string> GetSupportedIndustryCodes() => _profiles.Keys.ToArray();
    }
}
