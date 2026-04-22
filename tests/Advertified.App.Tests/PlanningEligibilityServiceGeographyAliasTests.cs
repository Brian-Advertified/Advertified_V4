using Advertified.App.Contracts.Campaigns;
using Advertified.App.Domain.Campaigns;
using Advertified.App.Configuration;
using Advertified.App.Services;
using FluentAssertions;

namespace Advertified.App.Tests;

public sealed class PlanningEligibilityServiceGeographyAliasTests
{
    private static PlanningPolicyService CreatePolicyService()
    {
        // Mirror appsettings defaults so tests behave like production policies.
        var options = new PlanningPolicyOptions
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
        };

        return new PlanningPolicyService(new PlanningPolicySnapshotProvider(options));
    }

    [Fact]
    public void FilterEligibleCandidates_TreatsSowetoAsJohannesburgForLocalMatching()
    {
        var policyService = CreatePolicyService();
        var service = new PlanningEligibilityService(policyService, new TestBroadcastMasterDataService());
        var request = new CampaignPlanningRequest
        {
            SelectedBudget = 50000m,
            GeographyScope = "local",
            Cities = new List<string> { "Johannesburg" }
        };

        var candidates = new List<InventoryCandidate>
        {
            new()
            {
                SourceId = Guid.NewGuid(),
                SourceType = "ooh",
                DisplayName = "Soweto Billboard",
                MediaType = "OOH",
                City = "Soweto",
                Area = "Soweto",
                Cost = 10000m,
                IsAvailable = true
            }
        };

        var result = service.FilterEligibleCandidates(candidates, request);
        result.Candidates.Should().HaveCount(1);
    }

    [Fact]
    public void FilterEligibleCandidates_WhenSuburbsSpecified_RequiresSuburbMatchWithinRequestedCity()
    {
        var policyService = CreatePolicyService();
        var service = new PlanningEligibilityService(policyService, new TestBroadcastMasterDataService());
        var request = new CampaignPlanningRequest
        {
            SelectedBudget = 100000m,
            GeographyScope = "local",
            Cities = new List<string> { "Johannesburg" },
            Suburbs = new List<string> { "DiepKloof, Soweto" }
        };

        var matchingCandidate = new InventoryCandidate
        {
            SourceId = Guid.NewGuid(),
            SourceType = "ooh",
            DisplayName = "DiepKloof Billboard",
            MediaType = "OOH",
            City = "Johannesburg",
            Suburb = "DiepKloof, Soweto",
            Area = "DiepKloof, Soweto",
            Cost = 45000m,
            IsAvailable = true
        };

        var nonMatchingSuburbSameCity = new InventoryCandidate
        {
            SourceId = Guid.NewGuid(),
            SourceType = "ooh",
            DisplayName = "Rosebank Billboard",
            MediaType = "OOH",
            City = "Johannesburg",
            Suburb = "Rosebank",
            Area = "Rosebank",
            Cost = 45000m,
            IsAvailable = true
        };

        var suburbMatchWrongCity = new InventoryCandidate
        {
            SourceId = Guid.NewGuid(),
            SourceType = "ooh",
            DisplayName = "Pretoria DiepKloof Billboard",
            MediaType = "OOH",
            City = "Pretoria",
            Suburb = "DiepKloof, Soweto",
            Area = "DiepKloof, Soweto",
            Cost = 45000m,
            IsAvailable = true
        };

        var result = service.FilterEligibleCandidates(new List<InventoryCandidate>
        {
            matchingCandidate,
            nonMatchingSuburbSameCity,
            suburbMatchWrongCity
        }, request);

        result.Candidates.Should().ContainSingle(x => x.DisplayName == "DiepKloof Billboard");
    }

    [Fact]
    public void FilterEligibleCandidates_WhenSuburbsSpecified_DoesNotExcludeRadioWhenCityMatches()
    {
        var policyService = CreatePolicyService();
        var service = new PlanningEligibilityService(policyService, new TestBroadcastMasterDataService());
        var request = new CampaignPlanningRequest
        {
            SelectedBudget = 100000m,
            GeographyScope = "local",
            Cities = new List<string> { "Johannesburg" },
            Suburbs = new List<string> { "DiepKloof, Soweto" }
        };

        var radioCandidate = new InventoryCandidate
        {
            SourceId = Guid.NewGuid(),
            SourceType = "radio_slot",
            DisplayName = "Kaya 959 - spot",
            MediaType = "Radio",
            City = null,
            MarketScope = "regional",
            Cost = 10000m,
            IsAvailable = true,
            Metadata = new Dictionary<string, object?>
            {
                ["cityLabels"] = new[] { "Johannesburg" }
            }
        };

        var result = service.FilterEligibleCandidates(new List<InventoryCandidate> { radioCandidate }, request);
        result.Candidates.Should().HaveCount(1);
    }

    [Fact]
    public void FilterEligibleCandidates_WhenSuburbsSpecified_AllowsOohWithinRadiusEvenIfSuburbTextDiffers()
    {
        var policyService = CreatePolicyService();
        var service = new PlanningEligibilityService(policyService, new TestBroadcastMasterDataService());
        var request = new CampaignPlanningRequest
        {
            SelectedBudget = 100000m,
            GeographyScope = "local",
            Cities = new List<string> { "Johannesburg" },
            Suburbs = new List<string> { "DiepKloof, Soweto" },
            TargetLatitude = -26.2497583,
            TargetLongitude = 27.9539444
        };

        var nearCandidate = new InventoryCandidate
        {
            SourceId = Guid.NewGuid(),
            SourceType = "ooh",
            DisplayName = "Nearby Billboard",
            MediaType = "OOH",
            City = "Johannesburg",
            Suburb = "Some Other Suburb",
            Area = "Some Other Suburb",
            Latitude = -26.2498,
            Longitude = 27.9540,
            Cost = 45000m,
            IsAvailable = true
        };

        var farCandidate = new InventoryCandidate
        {
            SourceId = Guid.NewGuid(),
            SourceType = "ooh",
            DisplayName = "Far Billboard",
            MediaType = "OOH",
            City = "Johannesburg",
            Suburb = "Some Other Suburb",
            Area = "Some Other Suburb",
            Latitude = -26.0,
            Longitude = 28.5,
            Cost = 45000m,
            IsAvailable = true
        };

        var result = service.FilterEligibleCandidates(new List<InventoryCandidate> { nearCandidate, farCandidate }, request);
        result.Candidates.Should().ContainSingle(x => x.DisplayName == "Nearby Billboard");
    }

    [Fact]
    public void FilterEligibleCandidates_WhenCitySpecified_AllowsOohWithinRadiusEvenIfSuburbTextDiffers()
    {
        var policyService = CreatePolicyService();
        var service = new PlanningEligibilityService(policyService, new TestBroadcastMasterDataService());
        var request = new CampaignPlanningRequest
        {
            SelectedBudget = 100000m,
            GeographyScope = "local",
            Cities = new List<string> { "Johannesburg" },
            TargetLatitude = -26.2041,
            TargetLongitude = 28.0473
        };

        var nearCandidate = new InventoryCandidate
        {
            SourceId = Guid.NewGuid(),
            SourceType = "ooh",
            DisplayName = "Soweto Billboard",
            MediaType = "billboard",
            City = "Unknown",
            Suburb = "Diepkloof",
            Area = "Diepkloof",
            Latitude = -26.2485,
            Longitude = 27.9449,
            Cost = 45000m,
            IsAvailable = true
        };

        var farCandidate = new InventoryCandidate
        {
            SourceId = Guid.NewGuid(),
            SourceType = "ooh",
            DisplayName = "Cape Town Billboard",
            MediaType = "billboard",
            City = "Unknown",
            Suburb = "Far away",
            Area = "Far away",
            Latitude = -33.9249,
            Longitude = 18.4241,
            Cost = 45000m,
            IsAvailable = true
        };

        var result = service.FilterEligibleCandidates(new List<InventoryCandidate> { nearCandidate, farCandidate }, request);
        result.Candidates.Should().ContainSingle(x => x.DisplayName == "Soweto Billboard");
    }

    [Theory]
    [InlineData("billboard")]
    [InlineData("digital_screen")]
    public void FilterEligibleCandidates_WhenSuburbsSpecified_AllowsNormalizedOohChannelsWithinRadius(string mediaType)
    {
        var policyService = CreatePolicyService();
        var service = new PlanningEligibilityService(policyService, new TestBroadcastMasterDataService());
        var request = new CampaignPlanningRequest
        {
            SelectedBudget = 100000m,
            GeographyScope = "local",
            Cities = new List<string> { "Johannesburg" },
            Suburbs = new List<string> { "DiepKloof, Soweto" },
            TargetLatitude = -26.2497583,
            TargetLongitude = 27.9539444
        };

        var nearCandidate = new InventoryCandidate
        {
            SourceId = Guid.NewGuid(),
            SourceType = "ooh",
            DisplayName = $"{mediaType} near target",
            MediaType = mediaType,
            City = "Johannesburg",
            Suburb = "Some Other Suburb",
            Area = "Some Other Suburb",
            Latitude = -26.2498,
            Longitude = 27.9540,
            Cost = 45000m,
            IsAvailable = true
        };

        var farCandidate = new InventoryCandidate
        {
            SourceId = Guid.NewGuid(),
            SourceType = "ooh",
            DisplayName = $"{mediaType} far away",
            MediaType = mediaType,
            City = "Johannesburg",
            Suburb = "Some Other Suburb",
            Area = "Some Other Suburb",
            Latitude = -26.0,
            Longitude = 28.5,
            Cost = 45000m,
            IsAvailable = true
        };

        var result = service.FilterEligibleCandidates(new List<InventoryCandidate> { nearCandidate, farCandidate }, request);
        result.Candidates.Should().ContainSingle(x => x.DisplayName == $"{mediaType} near target");
    }

    [Fact]
    public void FilterEligibleCandidates_AllowsNationalDigitalForProvincialBriefs()
    {
        var policyService = CreatePolicyService();
        var service = new PlanningEligibilityService(policyService, new TestBroadcastMasterDataService());
        var request = new CampaignPlanningRequest
        {
            SelectedBudget = 100000m,
            GeographyScope = "provincial",
            Provinces = new List<string> { "Western Cape" }
        };

        var digitalCandidate = new InventoryCandidate
        {
            SourceId = Guid.NewGuid(),
            SourceType = "digital_package",
            DisplayName = "Meta - Awareness benchmark",
            MediaType = "Digital",
            MarketScope = "national",
            Cost = 15000m,
            IsAvailable = true
        };

        var result = service.FilterEligibleCandidates(new List<InventoryCandidate> { digitalCandidate }, request);
        result.Candidates.Should().ContainSingle(x => x.DisplayName == "Meta - Awareness benchmark");
    }

    [Fact]
    public void FilterEligibleCandidates_ProvincialBroadcast_DoesNotUseSpilloverProvinceCodesAsPrimaryMatch()
    {
        var policyService = CreatePolicyService();
        var service = new PlanningEligibilityService(policyService, new TestBroadcastMasterDataService());
        var request = new CampaignPlanningRequest
        {
            SelectedBudget = 100000m,
            GeographyScope = "provincial",
            Provinces = new List<string> { "Gauteng" }
        };

        var spilloverRegionalStation = new InventoryCandidate
        {
            SourceId = Guid.NewGuid(),
            SourceType = "radio_slot",
            DisplayName = "Munghana Lonene FM - spot",
            MediaType = "Radio",
            Province = "Limpopo",
            RegionClusterCode = "Limpopo",
            MarketScope = "regional",
            Cost = 9240m,
            IsAvailable = true,
            Metadata = new Dictionary<string, object?>
            {
                ["provinceCodes"] = new[] { "limpopo", "mpumalanga", "gauteng", "north_west" }
            }
        };

        var result = service.FilterEligibleCandidates(new List<InventoryCandidate> { spilloverRegionalStation }, request);
        result.Candidates.Should().BeEmpty();
    }
}
