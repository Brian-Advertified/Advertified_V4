using Advertified.App.Configuration;
using Advertified.App.Contracts.Campaigns;
using Advertified.App.Domain.Campaigns;
using Advertified.App.Services;
using Advertified.App.Services.Abstractions;
using FluentAssertions;

namespace Advertified.App.Tests;

public sealed class MediaPlanningEngineLocalSuburbRadioTests
{
    private static readonly Guid SowetoCampaignId = Guid.Parse("a70c92aa-b837-4aab-81ea-c84e865084c3");

    [Fact]
    public async Task GenerateAsync_WhenLocalSuburbSelected_PrefersRegionalRadioOverNational()
    {
        var repository = new StubPlanningInventoryRepository
        {
            RadioSlotCandidates = new List<InventoryCandidate>
            {
                new()
                {
                    SourceId = Guid.NewGuid(),
                    SourceType = "radio_slot",
                    DisplayName = "Kaya 959 - spot",
                    MediaType = "Radio",
                    MarketScope = "regional",
                    Cost = 12000m,
                    IsAvailable = true,
                    Metadata = new Dictionary<string, object?>
                    {
                        ["cityLabels"] = new[] { "Johannesburg" }
                    }
                },
                new()
                {
                    SourceId = Guid.NewGuid(),
                    SourceType = "radio_slot",
                    DisplayName = "Metro FM - spot",
                    MediaType = "Radio",
                    MarketScope = "national",
                    Cost = 12000m,
                    IsAvailable = true
                }
            }
        };

        var engine = CreateEngine(repository);
        var request = new CampaignPlanningRequest
        {
            CampaignId = SowetoCampaignId,
            SelectedBudget = 100000m,
            GeographyScope = "local",
            Cities = new List<string> { "Johannesburg" },
            Suburbs = new List<string> { "DiepKloof, Soweto" },
            PreferredMediaTypes = new List<string> { "radio" },
            MaxMediaItems = 1
        };

        var result = await engine.GenerateAsync(request, CancellationToken.None);

        result.RecommendedPlan.Should().ContainSingle();
        result.RecommendedPlan[0].MediaType.Should().Be("Radio");
        result.RecommendedPlan[0].DisplayName.Should().Contain("Kaya 959");
    }

    [Fact]
    public async Task GenerateAsync_WhenSuburbTokenMatchesBroadcastCityLabels_PrefersJoziOverKaya()
    {
        var policyService = CreatePolicyService();
        var scoreService = new PlanningScoreService(policyService, new TestBroadcastMasterDataService());

        var repository = new StubPlanningInventoryRepository
        {
            RadioSlotCandidates = new List<InventoryCandidate>
            {
                new()
                {
                    SourceId = Guid.NewGuid(),
                    SourceType = "radio_slot",
                    DisplayName = "Kaya 959 - spot",
                    MediaType = "Radio",
                    MarketScope = "regional",
                    Cost = 12000m,
                    IsAvailable = true,
                    Metadata = new Dictionary<string, object?>
                    {
                        ["cityLabels"] = new[] { "Johannesburg", "Gauteng" }
                    }
                },
                new()
                {
                    SourceId = Guid.NewGuid(),
                    SourceType = "radio_slot",
                    DisplayName = "Jozi FM - spot",
                    MediaType = "Radio",
                    MarketScope = "regional",
                    Cost = 12000m,
                    IsAvailable = true,
                    Metadata = new Dictionary<string, object?>
                    {
                        ["cityLabels"] = new[] { "Johannesburg", "Soweto" }
                    }
                }
            }
        };

        var engine = CreateEngine(repository, policyService);
        var request = new CampaignPlanningRequest
        {
            CampaignId = SowetoCampaignId,
            SelectedBudget = 100000m,
            GeographyScope = "local",
            Cities = new List<string> { "Johannesburg" },
            Suburbs = new List<string> { "DiepKloof, Soweto" },
            PreferredMediaTypes = new List<string> { "radio" },
            MaxMediaItems = 1
        };

        var kaya = repository.RadioSlotCandidates.Single(x => x.DisplayName.StartsWith("Kaya", StringComparison.OrdinalIgnoreCase));
        var jozi = repository.RadioSlotCandidates.Single(x => x.DisplayName.StartsWith("Jozi", StringComparison.OrdinalIgnoreCase));
        scoreService.GeoScore(jozi, request).Should().BeGreaterThan(scoreService.GeoScore(kaya, request));

        var result = await engine.GenerateAsync(request, CancellationToken.None);

        result.RecommendedPlan.Should().ContainSingle();
        result.RecommendedPlan[0].MediaType.Should().Be("Radio");
        result.RecommendedPlan[0].DisplayName.Should().Contain("Jozi FM");
    }

    [Fact]
    public async Task GenerateAsync_WhenNoBroadcastMatchesSuburbToken_FallsBackToCityAndCoverage()
    {
        var repository = new StubPlanningInventoryRepository
        {
            RadioSlotCandidates = new List<InventoryCandidate>
            {
                new()
                {
                    SourceId = Guid.NewGuid(),
                    SourceType = "radio_slot",
                    DisplayName = "Kaya 959 - spot",
                    MediaType = "Radio",
                    MarketScope = "regional",
                    Cost = 12000m,
                    IsAvailable = true,
                    Metadata = new Dictionary<string, object?>
                    {
                        ["cityLabels"] = new[] { "Johannesburg", "Gauteng" }
                    }
                },
                new()
                {
                    SourceId = Guid.NewGuid(),
                    SourceType = "radio_slot",
                    DisplayName = "Metro FM - spot",
                    MediaType = "Radio",
                    MarketScope = "national",
                    Cost = 12000m,
                    IsAvailable = true
                }
            }
        };

        var engine = CreateEngine(repository);
        var request = new CampaignPlanningRequest
        {
            CampaignId = SowetoCampaignId,
            SelectedBudget = 100000m,
            GeographyScope = "local",
            Cities = new List<string> { "Johannesburg" },
            Suburbs = new List<string> { "Soshanguve" }, // no station has this suburb token
            PreferredMediaTypes = new List<string> { "radio" },
            MaxMediaItems = 1
        };

        var result = await engine.GenerateAsync(request, CancellationToken.None);

        result.RecommendedPlan.Should().ContainSingle();
        result.RecommendedPlan[0].MediaType.Should().Be("Radio");
        result.RecommendedPlan[0].DisplayName.Should().Contain("Kaya 959");
    }

    [Fact]
    public async Task GenerateAsync_ForProvincialBrief_RejectsSpilloverRegionalRadioAndUsesNationalAlternative()
    {
        var repository = new StubPlanningInventoryRepository
        {
            RadioSlotCandidates = new List<InventoryCandidate>
            {
                new()
                {
                    SourceId = Guid.NewGuid(),
                    SourceType = "radio_slot",
                    DisplayName = "Munghana Lonene FM - spot",
                    MediaType = "Radio",
                    MarketScope = "regional",
                    Province = "Limpopo",
                    RegionClusterCode = "Limpopo",
                    Cost = 9240m,
                    IsAvailable = true,
                    Metadata = new Dictionary<string, object?>
                    {
                        ["provinceCodes"] = new[] { "limpopo", "mpumalanga", "gauteng" },
                        ["language"] = "Xitsonga"
                    }
                },
                new()
                {
                    SourceId = Guid.NewGuid(),
                    SourceType = "radio_slot",
                    DisplayName = "SAfm - spot",
                    MediaType = "Radio",
                    MarketScope = "national",
                    Province = "national",
                    RegionClusterCode = "national",
                    Cost = 10000m,
                    IsAvailable = true,
                    Metadata = new Dictionary<string, object?>
                    {
                        ["language"] = "English"
                    }
                }
            }
        };

        var engine = CreateEngine(repository);
        var request = new CampaignPlanningRequest
        {
            CampaignId = SowetoCampaignId,
            SelectedBudget = 15000m,
            GeographyScope = "provincial",
            Provinces = new List<string> { "Gauteng" },
            PreferredMediaTypes = new List<string> { "radio" },
            MaxMediaItems = 1
        };

        var result = await engine.GenerateAsync(request, CancellationToken.None);

        result.RecommendedPlan.Should().ContainSingle();
        result.RecommendedPlan[0].MediaType.Should().Be("Radio");
        result.RecommendedPlan[0].DisplayName.Should().Contain("SAfm");
    }

    [Fact]
    public async Task GenerateAsync_WhenBudgetCannotFitAllRequestedChannels_PreservesHigherShareRadioOverLowerShareDigital()
    {
        var repository = new StubPlanningInventoryRepository
        {
            OohCandidates = new List<InventoryCandidate>
            {
                new()
                {
                    SourceId = Guid.NewGuid(),
                    SourceType = "ooh",
                    DisplayName = "Expensive OOH",
                    MediaType = "billboard",
                    Province = "Gauteng",
                    RegionClusterCode = "Gauteng",
                    Cost = 12600m,
                    IsAvailable = true
                },
                new()
                {
                    SourceId = Guid.NewGuid(),
                    SourceType = "ooh",
                    DisplayName = "Affordable OOH",
                    MediaType = "billboard",
                    Province = "Gauteng",
                    RegionClusterCode = "Gauteng",
                    Cost = 11550m,
                    IsAvailable = true
                }
            },
            RadioSlotCandidates = new List<InventoryCandidate>
            {
                new()
                {
                    SourceId = Guid.NewGuid(),
                    SourceType = "radio_slot",
                    DisplayName = "SAfm - spot",
                    MediaType = "Radio",
                    MarketScope = "national",
                    Province = "national",
                    RegionClusterCode = "national",
                    Cost = 13200m,
                    IsAvailable = true,
                    Metadata = new Dictionary<string, object?>
                    {
                        ["language"] = "English"
                    }
                }
            },
            DigitalCandidates = new List<InventoryCandidate>
            {
                new()
                {
                    SourceId = Guid.NewGuid(),
                    SourceType = "digital_package",
                    DisplayName = "Meta starter",
                    MediaType = "digital",
                    Cost = 8332.50m,
                    IsAvailable = true
                }
            }
        };

        var engine = CreateEngine(repository);
        var request = new CampaignPlanningRequest
        {
            CampaignId = SowetoCampaignId,
            SelectedBudget = 25000m,
            GeographyScope = "provincial",
            Provinces = new List<string> { "Gauteng" },
            PreferredMediaTypes = new List<string> { "ooh", "radio", "digital" },
            TargetOohShare = 60,
            TargetRadioShare = 32,
            TargetDigitalShare = 8
        };

        var result = await engine.GenerateAsync(request, CancellationToken.None);

        result.RecommendedPlan.Select(item => item.DisplayName)
            .Should()
            .Contain("Affordable OOH")
            .And.Contain("SAfm - spot");
        result.RecommendedPlan.Select(item => item.DisplayName)
            .Should()
            .NotContain("Meta starter");
        result.FallbackFlags.Should().Contain("preferred_media_unfulfilled:digital");
        result.FallbackFlags.Should().NotContain("preferred_media_unfulfilled:radio");
    }

    private static PlanningPolicyService CreatePolicyService()
    {
        var snapshotProvider = new PlanningPolicySnapshotProvider(new PlanningPolicyOptions
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
        });
        return new PlanningPolicyService(snapshotProvider);
    }

    private static MediaPlanningEngine CreateEngine(IPlanningInventoryRepository repository)
    {
        var policyService = CreatePolicyService();
        return CreateEngine(repository, policyService);
    }

    private static MediaPlanningEngine CreateEngine(IPlanningInventoryRepository repository, PlanningPolicyService policyService)
    {
        var candidateLoader = new PlanningCandidateLoader(repository);
        var eligibilityService = new PlanningEligibilityService(policyService, new TestBroadcastMasterDataService());
        var planBuilder = new RecommendationPlanBuilder(policyService, new TestBroadcastMasterDataService());
        var scoreService = new PlanningScoreService(policyService, new TestBroadcastMasterDataService());
        var explainabilityService = new RecommendationExplainabilityService(scoreService, policyService);
        return new MediaPlanningEngine(candidateLoader, eligibilityService, planBuilder, explainabilityService, policyService);
    }

    private sealed class StubPlanningInventoryRepository : IPlanningInventoryRepository
    {
        public List<InventoryCandidate> OohCandidates { get; set; } = new();
        public List<InventoryCandidate> DigitalCandidates { get; set; } = new();
        public List<InventoryCandidate> RadioSlotCandidates { get; set; } = new();
        public List<InventoryCandidate> RadioPackageCandidates { get; set; } = new();
        public List<InventoryCandidate> TvCandidates { get; set; } = new();

        public Task<List<InventoryCandidate>> GetOohCandidatesAsync(CampaignPlanningRequest request, CancellationToken cancellationToken)
            => Task.FromResult(OohCandidates);

        public Task<List<InventoryCandidate>> GetDigitalCandidatesAsync(CampaignPlanningRequest request, CancellationToken cancellationToken)
            => Task.FromResult(DigitalCandidates);

        public Task<BroadcastInventoryCandidateSet> GetBroadcastCandidatesAsync(CampaignPlanningRequest request, CancellationToken cancellationToken)
            => Task.FromResult(new BroadcastInventoryCandidateSet(RadioSlotCandidates, RadioPackageCandidates, TvCandidates));

        public Task<List<InventoryCandidate>> GetRadioSlotCandidatesAsync(CampaignPlanningRequest request, CancellationToken cancellationToken)
            => Task.FromResult(RadioSlotCandidates);

        public Task<List<InventoryCandidate>> GetRadioPackageCandidatesAsync(CampaignPlanningRequest request, CancellationToken cancellationToken)
            => Task.FromResult(RadioPackageCandidates);

        public Task<List<InventoryCandidate>> GetTvCandidatesAsync(CampaignPlanningRequest request, CancellationToken cancellationToken)
            => Task.FromResult(TvCandidates);
    }
}
