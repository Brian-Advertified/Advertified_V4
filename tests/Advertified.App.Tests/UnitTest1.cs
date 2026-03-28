using Advertified.App.Contracts.Auth;
using Advertified.App.Contracts.Campaigns;
using Advertified.App.Domain.Campaigns;
using Advertified.App.Services;
using Advertified.App.Services.Abstractions;
using Advertified.App.Validation;
using FluentAssertions;

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

        var engine = new MediaPlanningEngine(repository);
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
    public async Task GenerateAsync_CreatesUpsellsWhenAdditionalBudgetExists()
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

        var engine = new MediaPlanningEngine(repository);
        var request = new CampaignPlanningRequest
        {
            CampaignId = Guid.NewGuid(),
            SelectedBudget = 20000m,
            OpenToUpsell = true,
            AdditionalBudget = 7000m,
            MaxMediaItems = 5
        };

        var result = await engine.GenerateAsync(request, CancellationToken.None);

        result.Upsells.Should().NotBeEmpty();
        result.UpsellTotal.Should().BeGreaterThan(0m);
        result.Rationale.Should().Contain("Plan built within budget");
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

        var engine = new MediaPlanningEngine(repository);
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

    private sealed class StubPlanningInventoryRepository : IPlanningInventoryRepository
    {
        public List<InventoryCandidate> OohCandidates { get; set; } = new();
        public List<InventoryCandidate> RadioSlotCandidates { get; set; } = new();
        public List<InventoryCandidate> RadioPackageCandidates { get; set; } = new();

        public Task<List<InventoryCandidate>> GetOohCandidatesAsync(CampaignPlanningRequest request, CancellationToken cancellationToken)
            => Task.FromResult(OohCandidates);

        public Task<List<InventoryCandidate>> GetRadioSlotCandidatesAsync(CampaignPlanningRequest request, CancellationToken cancellationToken)
            => Task.FromResult(RadioSlotCandidates);

        public Task<List<InventoryCandidate>> GetRadioPackageCandidatesAsync(CampaignPlanningRequest request, CancellationToken cancellationToken)
            => Task.FromResult(RadioPackageCandidates);
    }
}
