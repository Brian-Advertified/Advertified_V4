using Advertified.App.Contracts.Auth;
using Advertified.App.Contracts.Campaigns;
using Advertified.App.Configuration;
using Advertified.App.Controllers;
using Advertified.App.Domain.Campaigns;
using Advertified.App.Data.Entities;
using Advertified.App.Services;
using Advertified.App.Services.Abstractions;
using Advertified.App.Validation;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Text.Json;
using CampaignBriefEntity = Advertified.App.Data.Entities.CampaignBrief;
using CampaignEntity = Advertified.App.Data.Entities.Campaign;
using PackageBandEntity = Advertified.App.Data.Entities.PackageBand;
using PackageOrderEntity = Advertified.App.Data.Entities.PackageOrder;

namespace Advertified.App.Tests;

public class RegisterRequestValidatorTests
{
    [Fact]
    public async Task ValidateAsync_AllowsValidSouthAfricanCitizenRequest()
    {
        var validator = new RegisterRequestValidator();
        var request = new RegisterRequest
        {
            FullName = "Test User",
            Email = "test.user@example.com",
            Phone = "0821234567",
            IsSouthAfricanCitizen = true,
            Password = "StrongPass!123",
            ConfirmPassword = "StrongPass!123",
            BusinessName = "Advertified Holdings",
            BusinessType = "pty_ltd",
            RegistrationNumber = "2024/123456/07",
            Industry = "marketing",
            AnnualRevenueBand = "r1m_r5m",
            StreetAddress = "1 Long Street",
            City = "Cape Town",
            Province = "Western Cape",
            SaIdNumber = "9001011234088"
        };

        var result = await validator.ValidateAsync(request);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateAsync_RejectsWeakPassword()
    {
        var validator = new RegisterRequestValidator();
        var request = new RegisterRequest
        {
            FullName = "Test User",
            Email = "test.user@example.com",
            Phone = "0821234567",
            IsSouthAfricanCitizen = true,
            Password = "weak",
            ConfirmPassword = "weak",
            BusinessName = "Advertified Holdings",
            BusinessType = "pty_ltd",
            RegistrationNumber = "2024/123456/07",
            Industry = "marketing",
            AnnualRevenueBand = "r1m_r5m",
            StreetAddress = "1 Long Street",
            City = "Cape Town",
            Province = "Western Cape",
            SaIdNumber = "9001011234088"
        };

        var result = await validator.ValidateAsync(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.PropertyName == nameof(RegisterRequest.Password));
    }
}

public class SaveCampaignBriefRequestValidatorTests
{
    [Fact]
    public async Task ValidateAsync_RejectsInvalidAgeRange()
    {
        var validator = new SaveCampaignBriefRequestValidator();
        var request = new SaveCampaignBriefRequest
        {
            Objective = "awareness",
            GeographyScope = "national",
            TargetAgeMin = 45,
            TargetAgeMax = 30
        };

        var result = await validator.ValidateAsync(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.ErrorMessage == "Target age range is invalid.");
    }
}

public class CampaignPlanningRequestValidatorTests
{
    [Fact]
    public async Task ValidateAsync_RejectsUnsupportedPreferredMediaType()
    {
        var validator = new CampaignPlanningRequestValidator();
        var request = new CampaignPlanningRequest
        {
            CampaignId = Guid.NewGuid(),
            SelectedBudget = 50000m,
            PreferredMediaTypes = new List<string> { "tv" }
        };

        var result = await validator.ValidateAsync(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.ErrorMessage == "Preferred media types contain an unsupported value.");
    }

    [Fact]
    public async Task ValidateAsync_RejectsNonPositiveBudget()
    {
        var validator = new CampaignPlanningRequestValidator();
        var request = new CampaignPlanningRequest
        {
            CampaignId = Guid.NewGuid(),
            SelectedBudget = 0m
        };

        var result = await validator.ValidateAsync(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.PropertyName == nameof(CampaignPlanningRequest.SelectedBudget));
    }
}

public class MediaPlanningEngineTests
{
    [Fact]
    public async Task GenerateAsync_ExcludesBlockedMediaTypesAndBuildsPlanWithinBudget()
    {
        var repository = new StubPlanningInventoryRepository
        {
            OohCandidates = new List<InventoryCandidate>
            {
                new()
                {
                    SourceId = Guid.NewGuid(),
                    SourceType = "ooh",
                    DisplayName = "Sandton Billboard",
                    MediaType = "OOH",
                    Province = "Gauteng",
                    City = "Johannesburg",
                    Cost = 12000m,
                    IsAvailable = true
                }
            },
            RadioSlotCandidates = new List<InventoryCandidate>
            {
                new()
                {
                    SourceId = Guid.NewGuid(),
                    SourceType = "radio_slot",
                    DisplayName = "Drive Time Slot",
                    MediaType = "Radio",
                    TimeBand = "drive",
                    DayType = "weekday",
                    SlotType = "commercial",
                    Language = "English",
                    Cost = 9000m,
                    IsAvailable = true
                }
            }
        };

        var engine = CreateEngine(repository);
        var request = new CampaignPlanningRequest
        {
            CampaignId = Guid.NewGuid(),
            SelectedBudget = 15000m,
            Provinces = new List<string> { "Gauteng" },
            ExcludedMediaTypes = new List<string> { "radio" },
            MaxMediaItems = 3
        };

        var result = await engine.GenerateAsync(request, CancellationToken.None);

        result.RecommendedPlan.Should().NotBeEmpty();
        result.RecommendedPlan.Should().OnlyContain(x => x.MediaType == "OOH");
        result.RecommendedPlanTotal.Should().BeLessThanOrEqualTo(request.SelectedBudget);
    }

    [Fact]
    public async Task GenerateAsync_RespectsAdditionalBudgetWhenUpsellCapacityExists()
    {
        var repository = new StubPlanningInventoryRepository
        {
            OohCandidates = new List<InventoryCandidate>
            {
                new()
                {
                    SourceId = Guid.NewGuid(),
                    SourceType = "ooh",
                    DisplayName = "Cape Town Billboard",
                    MediaType = "OOH",
                    Province = "Western Cape",
                    Cost = 12000m,
                    IsAvailable = true
                }
            },
            RadioSlotCandidates = new List<InventoryCandidate>
            {
                new()
                {
                    SourceId = Guid.NewGuid(),
                    SourceType = "radio_slot",
                    DisplayName = "Breakfast Radio Slot",
                    MediaType = "Radio",
                    TimeBand = "breakfast",
                    DayType = "weekday",
                    SlotType = "commercial",
                    Cost = 8000m,
                    IsAvailable = true
                }
            },
            RadioPackageCandidates = new List<InventoryCandidate>
            {
                new()
                {
                    SourceId = Guid.NewGuid(),
                    SourceType = "radio_package",
                    DisplayName = "Regional Radio Package",
                    MediaType = "Radio",
                    Subtype = "package",
                    Cost = 6000m,
                    IsAvailable = true,
                    PackageOnly = true
                }
            }
        };

        var engine = CreateEngine(repository);
        var request = new CampaignPlanningRequest
        {
            CampaignId = Guid.NewGuid(),
            SelectedBudget = 20000m,
            OpenToUpsell = true,
            AdditionalBudget = 7000m,
            MaxMediaItems = 5
        };

        var result = await engine.GenerateAsync(request, CancellationToken.None);

        result.RecommendedPlanTotal.Should().BeLessThanOrEqualTo(request.SelectedBudget);
        result.UpsellTotal.Should().BeLessThanOrEqualTo(request.AdditionalBudget!.Value);
        (result.RecommendedPlanTotal + result.UpsellTotal).Should().BeLessThanOrEqualTo(request.SelectedBudget + request.AdditionalBudget.Value);
        result.Rationale.Should().Contain("Plan built within budget");
    }

    [Fact]
    public async Task GenerateAsync_FillsTowardSelectedBudgetByIncreasingQuantities()
    {
        var repository = new StubPlanningInventoryRepository
        {
            OohCandidates = new List<InventoryCandidate>
            {
                new()
                {
                    SourceId = Guid.NewGuid(),
                    SourceType = "ooh",
                    DisplayName = "Sandton Billboard",
                    MediaType = "OOH",
                    Province = "Gauteng",
                    Cost = 12000m,
                    IsAvailable = true
                }
            },
            RadioSlotCandidates = new List<InventoryCandidate>
            {
                new()
                {
                    SourceId = Guid.NewGuid(),
                    SourceType = "radio_slot",
                    DisplayName = "Drive Time Slot",
                    MediaType = "Radio",
                    TimeBand = "drive",
                    DayType = "weekday",
                    SlotType = "commercial",
                    Cost = 9000m,
                    IsAvailable = true
                }
            }
        };

        var engine = CreateEngine(repository);
        var request = new CampaignPlanningRequest
        {
            CampaignId = Guid.NewGuid(),
            SelectedBudget = 30000m,
            MaxMediaItems = 4
        };

        var result = await engine.GenerateAsync(request, CancellationToken.None);

        result.RecommendedPlanTotal.Should().Be(30000m);
        result.RecommendedPlan.Sum(item => item.Quantity).Should().BeGreaterThan(2);
    }

    [Fact]
    public async Task GenerateAsync_DoesNotRepeatFixedSupplierPackagesToFillBudget()
    {
        var packageId = Guid.NewGuid();
        var slotId = Guid.NewGuid();
        var repository = new StubPlanningInventoryRepository
        {
            RadioPackageCandidates = new List<InventoryCandidate>
            {
                new()
                {
                    SourceId = packageId,
                    SourceType = "radio_package",
                    DisplayName = "Kaya Workzone Package",
                    MediaType = "Radio",
                    Cost = 18000m,
                    IsAvailable = true,
                    PackageOnly = true,
                    Metadata = new Dictionary<string, object?> { ["pricingModel"] = "package_total" }
                }
            },
            RadioSlotCandidates = new List<InventoryCandidate>
            {
                new()
                {
                    SourceId = slotId,
                    SourceType = "radio_slot",
                    DisplayName = "SABC Breakfast Spot",
                    MediaType = "Radio",
                    Cost = 1000m,
                    IsAvailable = true,
                    TimeBand = "breakfast",
                    SlotType = "commercial",
                    Metadata = new Dictionary<string, object?> { ["pricingModel"] = "per_spot_rate_card" }
                }
            }
        };

        var engine = CreateEngine(repository);
        var request = new CampaignPlanningRequest
        {
            CampaignId = Guid.NewGuid(),
            SelectedBudget = 20000m,
            PreferredMediaTypes = new List<string> { "radio" },
            MaxMediaItems = 5
        };

        var result = await engine.GenerateAsync(request, CancellationToken.None);

        result.RecommendedPlan.Should().ContainSingle(item => item.SourceId == packageId && item.Quantity == 1);
        result.RecommendedPlan.Should().ContainSingle(item => item.SourceId == slotId && item.Quantity == 2);
        result.RecommendedPlanTotal.Should().Be(20000m);
    }

    [Fact]
    public async Task GenerateAsync_RanksOohAheadOfRadioForMixedPreferredMedia()
    {
        var repository = new StubPlanningInventoryRepository
        {
            OohCandidates = new List<InventoryCandidate>
            {
                new()
                {
                    SourceId = Guid.NewGuid(),
                    SourceType = "ooh",
                    DisplayName = "Sandton Digital Billboard",
                    MediaType = "OOH",
                    Province = "Gauteng",
                    City = "Johannesburg",
                    Area = "Sandton",
                    Cost = 18000m,
                    IsAvailable = true
                }
            },
            RadioSlotCandidates = new List<InventoryCandidate>
            {
                new()
                {
                    SourceId = Guid.NewGuid(),
                    SourceType = "radio_slot",
                    DisplayName = "Kaya 959 - Drive",
                    MediaType = "Radio",
                    Province = "Gauteng",
                    TimeBand = "drive",
                    DayType = "weekday",
                    SlotType = "commercial",
                    Cost = 9000m,
                    IsAvailable = true
                }
            }
        };

        var engine = CreateEngine(repository);
        var request = new CampaignPlanningRequest
        {
            CampaignId = Guid.NewGuid(),
            SelectedBudget = 20000m,
            Provinces = new List<string> { "Gauteng" },
            Areas = new List<string> { "Sandton" },
            PreferredMediaTypes = new List<string> { "radio", "ooh" },
            MaxMediaItems = 4
        };

        var result = await engine.GenerateAsync(request, CancellationToken.None);

        result.RecommendedPlan.Should().NotBeEmpty();
        result.RecommendedPlan[0].MediaType.Should().Be("OOH");
    }

    [Fact]
    public async Task GenerateAsync_AddsFallbackFlagWhenPreferredMediaIsMissingFromRecommendation()
    {
        var repository = new StubPlanningInventoryRepository
        {
            RadioSlotCandidates = new List<InventoryCandidate>
            {
                new()
                {
                    SourceId = Guid.NewGuid(),
                    SourceType = "radio_slot",
                    DisplayName = "Kaya 959 - Drive",
                    MediaType = "Radio",
                    Province = "Gauteng",
                    TimeBand = "drive",
                    DayType = "weekday",
                    SlotType = "commercial",
                    Cost = 9000m,
                    IsAvailable = true
                }
            }
        };

        var engine = CreateEngine(repository);
        var request = new CampaignPlanningRequest
        {
            CampaignId = Guid.NewGuid(),
            SelectedBudget = 20000m,
            Provinces = new List<string> { "Gauteng" },
            PreferredMediaTypes = new List<string> { "radio", "ooh" },
            MaxMediaItems = 4
        };

        var result = await engine.GenerateAsync(request, CancellationToken.None);

        result.ManualReviewRequired.Should().BeTrue();
        result.FallbackFlags.Should().Contain("preferred_media_unfulfilled:ooh");
    }

    [Fact]
    public async Task GenerateAsync_ScaleBudgetPrefersNationalRadioCandidatesWhenAvailable()
    {
        var repository = new StubPlanningInventoryRepository
        {
            RadioSlotCandidates = new List<InventoryCandidate>
            {
                new()
                {
                    SourceId = Guid.NewGuid(),
                    SourceType = "radio_slot",
                    DisplayName = "Kaya 959 - Workzone",
                    MediaType = "Radio",
                    Cost = 12000m,
                    IsAvailable = true,
                    RegionClusterCode = "gauteng"
                },
                new()
                {
                    SourceId = Guid.NewGuid(),
                    SourceType = "radio_slot",
                    DisplayName = "Metro FM - Breakfast",
                    MediaType = "Radio",
                    Cost = 18000m,
                    IsAvailable = true,
                    MarketScope = "national",
                    MarketTier = "flagship",
                    IsFlagshipStation = true,
                    IsPremiumStation = true
                }
            }
        };

        var engine = CreateEngine(repository);
        var request = new CampaignPlanningRequest
        {
            CampaignId = Guid.NewGuid(),
            SelectedBudget = 250000m,
            PreferredMediaTypes = new List<string> { "radio" },
            MaxMediaItems = 3
        };

        var result = await engine.GenerateAsync(request, CancellationToken.None);

        result.RecommendedPlan.Should().ContainSingle();
        result.RecommendedPlan[0].DisplayName.Should().Contain("Metro FM");
    }

    [Fact]
    public async Task GenerateAsync_SetsFallbackFlagsWhenHigherBandRadioPolicyCannotBeSatisfied()
    {
        var repository = new StubPlanningInventoryRepository
        {
            RadioSlotCandidates = new List<InventoryCandidate>
            {
                new()
                {
                    SourceId = Guid.NewGuid(),
                    SourceType = "radio_slot",
                    DisplayName = "Kaya 959 - Workzone",
                    MediaType = "Radio",
                    Cost = 12000m,
                    IsAvailable = true,
                    RegionClusterCode = "gauteng",
                    MarketScope = "regional"
                }
            }
        };

        var engine = CreateEngine(repository);
        var request = new CampaignPlanningRequest
        {
            CampaignId = Guid.NewGuid(),
            SelectedBudget = 600000m,
            PreferredMediaTypes = new List<string> { "radio" },
            MaxMediaItems = 3
        };

        var result = await engine.GenerateAsync(request, CancellationToken.None);

        result.ManualReviewRequired.Should().BeTrue();
        result.FallbackFlags.Should().Contain("national_radio_inventory_insufficient");
        result.FallbackFlags.Should().Contain("policy_relaxed");
    }

    [Fact]
    public async Task GenerateAsync_SetsInventoryFallbackWhenNoEligibleCandidatesRemain()
    {
        var repository = new StubPlanningInventoryRepository
        {
            OohCandidates = new List<InventoryCandidate>
            {
                new()
                {
                    SourceId = Guid.NewGuid(),
                    SourceType = "ooh",
                    DisplayName = "Cape Town Billboard",
                    MediaType = "OOH",
                    Province = "Western Cape",
                    Cost = 12000m,
                    IsAvailable = true
                }
            }
        };

        var engine = CreateEngine(repository);
        var request = new CampaignPlanningRequest
        {
            CampaignId = Guid.NewGuid(),
            SelectedBudget = 20000m,
            Provinces = new List<string> { "Gauteng" },
            PreferredMediaTypes = new List<string> { "radio" },
            MaxMediaItems = 3
        };

        var result = await engine.GenerateAsync(request, CancellationToken.None);

        result.RecommendedPlan.Should().BeEmpty();
        result.ManualReviewRequired.Should().BeTrue();
        result.FallbackFlags.Should().Contain("inventory_insufficient");
        result.FallbackFlags.Should().Contain("no_recommendation_generated");
    }

    private static MediaPlanningEngine CreateEngine(IPlanningInventoryRepository repository)
    {
        return new MediaPlanningEngine(repository, new PlanningPolicySnapshotProvider(new PlanningPolicyOptions
        {
            Scale = new PackagePlanningPolicy
            {
                BudgetFloor = 150000m,
                MinimumNationalRadioCandidates = 1,
                RequireNationalCapableRadio = true,
                RequirePremiumNationalRadio = false,
                NationalRadioBonus = 12,
                NonNationalRadioPenalty = 8,
                RegionalRadioPenalty = 16
            },
            Dominance = new PackagePlanningPolicy
            {
                BudgetFloor = 500000m,
                MinimumNationalRadioCandidates = 2,
                RequireNationalCapableRadio = true,
                RequirePremiumNationalRadio = true,
                NationalRadioBonus = 18,
                NonNationalRadioPenalty = 12,
                RegionalRadioPenalty = 24
            }
        }));
    }

    private sealed class StubPlanningInventoryRepository : IPlanningInventoryRepository
    {
        public List<InventoryCandidate> OohCandidates { get; set; } = new();
        public List<InventoryCandidate> RadioSlotCandidates { get; set; } = new();
        public List<InventoryCandidate> RadioPackageCandidates { get; set; } = new();
        public List<InventoryCandidate> TvCandidates { get; set; } = new();

        public Task<List<InventoryCandidate>> GetOohCandidatesAsync(CampaignPlanningRequest request, CancellationToken cancellationToken)
            => Task.FromResult(OohCandidates);

        public Task<List<InventoryCandidate>> GetRadioSlotCandidatesAsync(CampaignPlanningRequest request, CancellationToken cancellationToken)
            => Task.FromResult(RadioSlotCandidates);

        public Task<List<InventoryCandidate>> GetRadioPackageCandidatesAsync(CampaignPlanningRequest request, CancellationToken cancellationToken)
            => Task.FromResult(RadioPackageCandidates);

        public Task<List<InventoryCandidate>> GetTvCandidatesAsync(CampaignPlanningRequest request, CancellationToken cancellationToken)
            => Task.FromResult(TvCandidates);
    }
}

public class CreativeStudioIntelligenceServiceTests
{
    [Fact]
    public async Task GenerateAsync_FallbackBuildsStructuredChannelAdaptationsWithVersions()
    {
        var service = new CreativeStudioIntelligenceService(
            new HttpClient(),
            Options.Create(new OpenAIOptions
            {
                Enabled = false,
                ApiKey = string.Empty,
                Model = "test-model"
            }),
            NullLogger<CreativeStudioIntelligenceService>.Instance);

        var campaign = new CampaignEntity
        {
            Id = Guid.NewGuid(),
            CampaignName = "Momentum Launch",
            User = new UserAccount
            {
                FullName = "Test User",
                BusinessProfile = new BusinessProfile
                {
                    BusinessName = "Advertified Labs"
                }
            },
            PackageBand = new PackageBandEntity
            {
                Name = "Growth"
            },
            PackageOrder = new PackageOrderEntity
            {
                Amount = 75000m,
                SelectedBudget = 75000m
            },
            CampaignRecommendations = new List<CampaignRecommendation>()
        };

        var brief = new CampaignBriefEntity
        {
            Objective = "Drive response",
            TargetAudienceNotes = "Busy operators who want faster growth",
            CreativeNotes = "Keep it premium but direct"
        };

        var request = new Advertified.App.Contracts.Creative.GenerateCreativeSystemRequest
        {
            Prompt = "Build a production-ready campaign system.",
            Channels = new[] { "Billboard", "TikTok", "Social Static" },
            Cta = "Book your rollout today"
        };

        var result = await service.GenerateAsync(campaign, brief, request, CancellationToken.None);

        result.ChannelAdaptations.Should().HaveCount(3);
        result.ChannelAdaptations.Should().OnlyContain(item => item.Versions.Count == 3);
        result.ChannelAdaptations.Should().OnlyContain(item => item.Sections.Count > 0);
        result.ChannelAdaptations.Should().OnlyContain(item => !string.IsNullOrWhiteSpace(item.AdapterPrompt));
        result.ChannelAdaptations.Should().Contain(item => item.Channel == "Social Static");
    }
}

public class BroadcastCostNormalizerTests
{
    [Fact]
    public void NormalizeRadioRate_ConvertsSpotRateToMonthlyEstimate()
    {
        var normalizer = new BroadcastCostNormalizer();

        var result = normalizer.NormalizeRadioRate(
            station: "Kaya 959",
            slotLabel: "Breakfast",
            groupName: "weekday",
            rawRateZar: 1000m);

        result.RawCostZar.Should().Be(1000m);
        result.MonthlyCostEstimateZar.Should().Be(20000m);
        result.CostType.Should().Be("radio_slot");
    }

    [Fact]
    public void NormalizeTvPackage_ConvertsMultiWeekPackageToMonthlyEstimate()
    {
        var normalizer = new BroadcastCostNormalizer();

        var result = normalizer.NormalizeTvPackage(
            station: "SABC 3",
            packageName: "Prime 8 Week Burst",
            investmentZar: 80000m,
            packageCostZar: null,
            costPerMonthZar: null,
            durationWeeks: 8,
            durationMonths: null);

        result.RawCostZar.Should().Be(80000m);
        result.MonthlyCostEstimateZar.Should().Be(40000m);
        result.CostType.Should().Be("tv_weekly_or_multi_week_package");
    }
}

public class PricingPolicyTests
{
    [Fact]
    public void CalculateChargedAmount_AddsHiddenAiStudioReserve()
    {
        var chargedAmount = Advertified.App.Support.PricingPolicy.CalculateChargedAmount(38000m, 0.10m);

        chargedAmount.Should().Be(41800m);
    }

    [Fact]
    public void ApplyMarkup_UsesConfiguredChannelPercentages()
    {
        var settings = new Advertified.App.Support.PricingSettingsSnapshot(0.10m, 0.05m, 0.10m, 0.10m);

        Advertified.App.Support.PricingPolicy.ApplyMarkup(1000m, "OOH", "billboard", settings).Should().Be(1050m);
        Advertified.App.Support.PricingPolicy.ApplyMarkup(1000m, "radio", null, settings).Should().Be(1100m);
        Advertified.App.Support.PricingPolicy.ApplyMarkup(1000m, "tv", null, settings).Should().Be(1100m);
    }
}

public class CampaignOperationsPolicyTests
{
    [Fact]
    public void BuildRefundSnapshot_BeforeWorkStarts_AllowsFullRefund()
    {
        var campaign = CreateCampaign(status: "paid", chargedAmount: 41800m, selectedBudget: 38000m);

        var snapshot = Advertified.App.Support.CampaignOperationsPolicy.BuildRefundSnapshot(campaign);

        snapshot.Stage.Should().Be("before_work_starts");
        snapshot.SuggestedRefundAmount.Should().Be(41800m);
    }

    [Fact]
    public void BuildRefundSnapshot_StrategyInProgress_RetainsAiStudioReserve()
    {
        var campaign = CreateCampaign(status: "planning_in_progress", chargedAmount: 41800m, selectedBudget: 38000m);

        var snapshot = Advertified.App.Support.CampaignOperationsPolicy.BuildRefundSnapshot(campaign);

        snapshot.Stage.Should().Be("strategy_in_progress");
        snapshot.SuggestedRefundAmount.Should().Be(38000m);
    }

    [Fact]
    public void BuildRefundSnapshot_AfterApproval_RequiresManualAmount()
    {
        var campaign = CreateCampaign(status: "creative_approved", chargedAmount: 41800m, selectedBudget: 38000m);

        var snapshot = Advertified.App.Support.CampaignOperationsPolicy.BuildRefundSnapshot(campaign);

        snapshot.Stage.Should().Be("post_delivery_or_live");
        snapshot.SuggestedRefundAmount.Should().Be(0m);
        snapshot.MaxManualRefundAmount.Should().Be(41800m);
    }

    [Fact]
    public void BuildScheduleSnapshot_ExtendsEndDateByPausedDays()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var campaign = CreateCampaign(status: "launched", chargedAmount: 100000m, selectedBudget: 90000m);
        campaign.CampaignBrief = new Advertified.App.Data.Entities.CampaignBrief
        {
            Id = Guid.NewGuid(),
            CampaignId = campaign.Id,
            Objective = "launch",
            GeographyScope = "regional",
            StartDate = today.AddDays(-1),
            EndDate = today.AddDays(9),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        campaign.TotalPausedDays = 2;

        var snapshot = Advertified.App.Support.CampaignOperationsPolicy.BuildScheduleSnapshot(campaign, today);

        snapshot.EffectiveEndDate.Should().Be(today.AddDays(11));
        snapshot.DaysLeft.Should().Be(12);
    }

    [Fact]
    public void BuildScheduleSnapshot_UsesBookedLiveWindowWhenAvailable()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var campaign = CreateCampaign(status: "launched", chargedAmount: 100000m, selectedBudget: 90000m);
        campaign.CampaignSupplierBookings.Add(new CampaignSupplierBooking
        {
            Id = Guid.NewGuid(),
            CampaignId = campaign.Id,
            SupplierOrStation = "Metro FM",
            Channel = "radio",
            BookingStatus = "booked",
            LiveFrom = today.AddDays(2),
            LiveTo = today.AddDays(16),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        var snapshot = Advertified.App.Support.CampaignOperationsPolicy.BuildScheduleSnapshot(campaign, today);

        snapshot.StartDate.Should().Be(today.AddDays(2));
        snapshot.EndDate.Should().Be(today.AddDays(16));
        snapshot.EffectiveEndDate.Should().Be(today.AddDays(16));
        snapshot.DaysLeft.Should().Be(17);
    }

    private static CampaignEntity CreateCampaign(string status, decimal chargedAmount, decimal selectedBudget)
    {
        var userId = Guid.NewGuid();
        return new CampaignEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            PackageOrderId = Guid.NewGuid(),
            PackageBandId = Guid.NewGuid(),
            CampaignName = "Operations campaign",
            Status = status,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            User = new UserAccount
            {
                Id = userId,
                FullName = "Brian Rapula",
                Email = "brian@example.com",
                Phone = "0821234567",
                PasswordHash = "hash",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            PackageBand = new PackageBandEntity
            {
                Id = Guid.NewGuid(),
                Code = "scale",
                Name = "Scale",
                MinBudget = 20000m,
                MaxBudget = 500000m,
                SortOrder = 1,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            },
            PackageOrder = new PackageOrderEntity
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                PackageBandId = Guid.NewGuid(),
                Amount = chargedAmount,
                SelectedBudget = selectedBudget,
                Currency = "ZAR",
                PaymentStatus = "paid",
                RefundStatus = "none",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }
        };
    }
}

public class ControllerMappingsTests
{
    [Fact]
    public void ToDetail_MapsManualReviewAndFallbackFlagsFromRecommendationRationale()
    {
        var userId = Guid.NewGuid();
        var campaign = new CampaignEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            PackageOrderId = Guid.NewGuid(),
            PackageBandId = Guid.NewGuid(),
            CampaignName = "Dominance campaign",
            Status = "planning_in_progress",
            CreatedAt = DateTime.UtcNow,
            User = new UserAccount
            {
                Id = userId,
                FullName = "Brian Rapula",
                Email = "brian@example.com",
                Phone = "0821234567",
                PasswordHash = "hash",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                BusinessProfile = new BusinessProfile
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    BusinessName = "Black Space PSG (Pty) Ltd",
                    BusinessType = "pty_ltd",
                    RegistrationNumber = "2024/123456/07",
                    Industry = "Health",
                    AnnualRevenueBand = "r1m_r5m",
                    StreetAddress = "1 Main Road",
                    City = "Johannesburg",
                    Province = "Gauteng",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                }
            },
            PackageBand = new PackageBandEntity
            {
                Id = Guid.NewGuid(),
                Code = "dominance",
                Name = "Dominance",
                MinBudget = 500000m,
                MaxBudget = 5000000m,
                SortOrder = 4,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            },
            PackageOrder = new PackageOrderEntity
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                PackageBandId = Guid.NewGuid(),
                Amount = 500000m,
                SelectedBudget = 500000m,
                Currency = "ZAR",
                PaymentStatus = "paid",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                User = new UserAccount
                {
                    Id = userId,
                    FullName = "Brian Rapula",
                    Email = "brian@example.com",
                    Phone = "0821234567",
                    PasswordHash = "hash",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                PackageBand = new PackageBandEntity
                {
                    Id = Guid.NewGuid(),
                    Code = "dominance",
                    Name = "Dominance",
                    MinBudget = 500000m,
                    MaxBudget = 5000000m,
                    SortOrder = 4,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                }
            }
        };

        campaign.CampaignRecommendations.Add(new CampaignRecommendation
        {
            Id = Guid.NewGuid(),
            CampaignId = campaign.Id,
            RecommendationType = "hybrid",
            GeneratedBy = "system",
            Status = "draft",
            TotalCost = 320000m,
            Summary = "Recommended 3 planned item(s) across Radio, OOH.",
            Rationale = string.Join(Environment.NewLine, new[]
            {
                "Plan built within budget and aligned to campaign geography.",
                "Manual review required: True",
                "Fallback flags: national_radio_inventory_insufficient, policy_relaxed"
            }),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        var response = campaign.ToDetail(userId);

        response.Recommendation.Should().NotBeNull();
        response.Recommendation!.ManualReviewRequired.Should().BeTrue();
        response.Recommendation.FallbackFlags.Should().Contain(new[] { "national_radio_inventory_insufficient", "policy_relaxed" });
        response.Recommendation.Rationale.Should().Be("Plan built within budget and aligned to campaign geography.");
    }

    [Fact]
    public void ToDetail_NormalizesSparseLegacyRecommendationItemMetadata()
    {
        var userId = Guid.NewGuid();
        var campaign = new CampaignEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            PackageOrderId = Guid.NewGuid(),
            PackageBandId = Guid.NewGuid(),
            CampaignName = "Legacy campaign",
            Status = "planning_in_progress",
            CreatedAt = DateTime.UtcNow,
            User = new UserAccount
            {
                Id = userId,
                FullName = "Brian Rapula",
                Email = "brian@example.com",
                Phone = "0821234567",
                PasswordHash = "hash",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            PackageBand = new PackageBandEntity
            {
                Id = Guid.NewGuid(),
                Code = "scale",
                Name = "Scale",
                MinBudget = 150000m,
                MaxBudget = 500000m,
                SortOrder = 3,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            },
            PackageOrder = new PackageOrderEntity
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                PackageBandId = Guid.NewGuid(),
                Amount = 250000m,
                SelectedBudget = 250000m,
                Currency = "ZAR",
                PaymentStatus = "paid",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                User = new UserAccount
                {
                    Id = userId,
                    FullName = "Brian Rapula",
                    Email = "brian@example.com",
                    Phone = "0821234567",
                    PasswordHash = "hash",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                PackageBand = new PackageBandEntity
                {
                    Id = Guid.NewGuid(),
                    Code = "scale",
                    Name = "Scale",
                    MinBudget = 150000m,
                    MaxBudget = 500000m,
                    SortOrder = 3,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                }
            }
        };

        var recommendation = new CampaignRecommendation
        {
            Id = Guid.NewGuid(),
            CampaignId = campaign.Id,
            RecommendationType = "ai_assisted",
            GeneratedBy = "system",
            Status = "draft",
            TotalCost = 25000m,
            Summary = "Legacy summary",
            Rationale = "Legacy rationale",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        recommendation.RecommendationItems.Add(new RecommendationItem
        {
            Id = Guid.NewGuid(),
            RecommendationId = recommendation.Id,
            InventoryType = "Radio",
            DisplayName = "Metro FM Breakfast",
            Quantity = 1,
            UnitCost = 25000m,
            TotalCost = 25000m,
            MetadataJson = JsonSerializer.Serialize(new
            {
                rationale = "Gauteng | English | Breakfast | Qty 1 | 30s | Strong geography match"
            }),
            CreatedAt = DateTime.UtcNow
        });

        campaign.CampaignRecommendations.Add(recommendation);

        var response = campaign.ToDetail(userId);
        var item = response.Recommendation!.Items.Should().ContainSingle().Subject;

        item.Region.Should().Be("Gauteng");
        item.Language.Should().Be("English");
        item.ShowDaypart.Should().Be("Breakfast");
        item.TimeBand.Should().Be("06:00-09:00");
        item.SlotType.Should().Be("Radio spot");
        item.Duration.Should().Be("30s");
    }

    [Fact]
    public void ToDetail_BuildsClientTimelineStates()
    {
        var userId = Guid.NewGuid();
        var campaign = new CampaignEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            PackageOrderId = Guid.NewGuid(),
            PackageBandId = Guid.NewGuid(),
            CampaignName = "Timeline campaign",
            Status = "review_ready",
            CreatedAt = DateTime.UtcNow,
            User = new UserAccount
            {
                Id = userId,
                FullName = "Brian Rapula",
                Email = "brian@example.com",
                Phone = "0821234567",
                PasswordHash = "hash",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            PackageBand = new PackageBandEntity
            {
                Id = Guid.NewGuid(),
                Code = "scale",
                Name = "Scale",
                MinBudget = 150000m,
                MaxBudget = 500000m,
                SortOrder = 3,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            },
            PackageOrder = new PackageOrderEntity
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                PackageBandId = Guid.NewGuid(),
                Amount = 250000m,
                SelectedBudget = 250000m,
                Currency = "ZAR",
                PaymentStatus = "paid",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                User = new UserAccount
                {
                    Id = userId,
                    FullName = "Brian Rapula",
                    Email = "brian@example.com",
                    Phone = "0821234567",
                    PasswordHash = "hash",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                PackageBand = new PackageBandEntity
                {
                    Id = Guid.NewGuid(),
                    Code = "scale",
                    Name = "Scale",
                    MinBudget = 150000m,
                    MaxBudget = 500000m,
                    SortOrder = 3,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                }
            },
            CampaignBrief = new Advertified.App.Data.Entities.CampaignBrief
            {
                Id = Guid.NewGuid(),
                CampaignId = Guid.NewGuid(),
                Objective = "launch",
                GeographyScope = "regional",
                SubmittedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }
        };

        campaign.CampaignRecommendations.Add(new CampaignRecommendation
        {
            Id = Guid.NewGuid(),
            CampaignId = campaign.Id,
            RecommendationType = "ai_assisted",
            GeneratedBy = "system",
            Status = "sent_to_client",
            TotalCost = 250000m,
            Summary = "Ready for review",
            Rationale = "Campaign ready for client review",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        var response = campaign.ToDetail(userId);

        response.Timeline.Should().HaveCount(8);
        response.Timeline[0].State.Should().Be("complete");
        response.Timeline[1].State.Should().Be("complete");
        response.Timeline[2].State.Should().Be("complete");
        response.Timeline[3].State.Should().Be("current");
        response.Timeline[4].State.Should().Be("upcoming");
        response.Timeline[5].State.Should().Be("upcoming");
        response.Timeline[6].State.Should().Be("upcoming");
        response.Timeline[7].State.Should().Be("upcoming");
    }
}

public class OpenAICampaignReasoningServiceTests
{
    [Fact]
    public async Task GenerateAsync_ReturnsNullWhenOpenAiIsDisabled()
    {
        var service = new OpenAICampaignReasoningService(
            new HttpClient { BaseAddress = new Uri("https://api.openai.com/v1/") },
            Options.Create(new OpenAIOptions
            {
                Enabled = false,
                ApiKey = string.Empty,
                BaseUrl = "https://api.openai.com/v1/",
                Model = "gpt-5-mini"
            }),
            NullLogger<OpenAICampaignReasoningService>.Instance);

        var result = await service.GenerateAsync(
            new CampaignEntity
            {
                Id = Guid.NewGuid(),
                CampaignName = "Dominance campaign",
                PackageBand = new PackageBandEntity
                {
                    Id = Guid.NewGuid(),
                    Code = "dominance",
                    Name = "Dominance",
                    MinBudget = 500000m,
                    MaxBudget = 5000000m,
                    SortOrder = 4,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                }
            },
            new Advertified.App.Data.Entities.CampaignBrief
            {
                Id = Guid.NewGuid(),
                CampaignId = Guid.NewGuid(),
                Objective = "launch",
                GeographyScope = "regional",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new CampaignPlanningRequest
            {
                CampaignId = Guid.NewGuid(),
                SelectedBudget = 250000m,
                PreferredMediaTypes = new List<string> { "radio", "ooh" },
                Provinces = new List<string> { "Gauteng" }
            },
            new RecommendationResult
            {
                RecommendedPlan = new List<PlannedItem>
                {
                    new PlannedItem
                    {
                        SourceId = Guid.NewGuid(),
                        SourceType = "radio_slot",
                        DisplayName = "Metro FM",
                        MediaType = "Radio",
                        UnitCost = 25000m,
                        Metadata = new Dictionary<string, object?>
                        {
                            ["selectionReasons"] = new[] { "Strong geography match", "Matches requested channel mix" },
                            ["confidenceScore"] = 0.85m
                        }
                    }
                },
                Rationale = "Plan built within budget.",
                ManualReviewRequired = false
            },
            CancellationToken.None);

        result.Should().BeNull();
    }
}
