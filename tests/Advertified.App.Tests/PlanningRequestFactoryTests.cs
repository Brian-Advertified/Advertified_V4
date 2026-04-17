using Advertified.App.Contracts.Campaigns;
using Advertified.App.Data.Entities;
using Advertified.App.Services;
using Advertified.App.Services.Abstractions;
using Advertified.App.Support;
using FluentAssertions;

namespace Advertified.App.Tests;

public sealed class PlanningRequestFactoryTests
{
    [Fact]
    public void FromCampaignBrief_GeocodesWhenPossible()
    {
        var factory = new PlanningRequestFactory(
            new CampaignPlanningTargetResolver(new TestGeocodingService()),
            new TestBusinessLocationResolver(),
            new TestPlanningBudgetAllocationService());
        var campaign = new Campaign
        {
            Id = Guid.NewGuid(),
            User = new UserAccount
            {
                BusinessProfile = new BusinessProfile
                {
                    BusinessName = "Origin Foods",
                    StreetAddress = "12 Fredman Drive",
                    City = "Sandton",
                    Province = "Gauteng"
                }
            },
            PackageOrder = new PackageOrder
            {
                Amount = 100000m,
                AiStudioReserveAmount = 0m
            }
        };
        var brief = new CampaignBrief
        {
            GeographyScope = "local",
            CitiesJson = "[\"Johannesburg\"]",
            SuburbsJson = "[\"DiepKloof, Soweto\"]"
        };

        var request = factory.FromCampaignBrief(campaign, brief, new GenerateRecommendationRequest { TargetRadioShare = 33 }, packageProfile: null);

        request.TargetLatitude.Should().Be(-26.2497583);
        request.TargetLongitude.Should().Be(27.9539444);
        request.GeographyScope.Should().Be("local");
        request.Cities.Should().Contain("Johannesburg");
        request.Suburbs.Should().Contain("DiepKloof, Soweto");
        request.BusinessLocation.Should().NotBeNull();
        request.BusinessLocation!.City.Should().Be("Sandton");
        request.Targeting.Should().NotBeNull();
        request.BudgetAllocation.Should().NotBeNull();
    }

    [Fact]
    public void FromCampaignBrief_AddsBusinessOriginToPriorityAreasForBroadCoverage()
    {
        var factory = new PlanningRequestFactory(
            new CampaignPlanningTargetResolver(new TestGeocodingService()),
            new TestBusinessLocationResolver(),
            new TestPlanningBudgetAllocationService());
        var campaign = new Campaign
        {
            Id = Guid.NewGuid(),
            PackageOrder = new PackageOrder
            {
                Amount = 100000m,
                AiStudioReserveAmount = 0m
            }
        };
        var brief = new CampaignBrief
        {
            GeographyScope = "provincial",
            ProvincesJson = "[\"Gauteng\"]",
            MustHaveAreasJson = "[\"Rosebank\"]"
        };

        var request = factory.FromCampaignBrief(campaign, brief, new GenerateRecommendationRequest(), packageProfile: null);

        request.Targeting.Should().NotBeNull();
        request.Targeting!.PriorityAreas.Should().Contain("Rosebank");
        request.Targeting.PriorityAreas.Should().Contain("Sandton");
        request.BudgetAllocation.Should().NotBeNull();
    }

    private sealed class TestGeocodingService : IGeocodingService
    {
        public GeocodingResolution ResolveLocation(string? rawLocation)
        {
            return rawLocation switch
            {
                "DiepKloof, Soweto" => new GeocodingResolution
                {
                    IsResolved = true,
                    CanonicalLocation = "DiepKloof, Soweto",
                    Latitude = -26.2497583,
                    Longitude = 27.9539444,
                    Source = "test"
                },
                "12 Fredman Drive, Sandton, Gauteng" => new GeocodingResolution
                {
                    IsResolved = true,
                    CanonicalLocation = "Sandton",
                    Latitude = -26.1076,
                    Longitude = 28.0567,
                    Source = "test"
                },
                _ => new GeocodingResolution()
            };
        }

        public GeocodingResolution ResolveCampaignTarget(CampaignPlanningRequest request)
        {
            return new GeocodingResolution
            {
                IsResolved = true,
                Latitude = -26.2497583,
                Longitude = 27.9539444,
                Source = "test"
            };
        }
    }

    private sealed class TestBusinessLocationResolver : ICampaignBusinessLocationResolver
    {
        public CampaignBusinessLocationResolution Resolve(Campaign campaign)
        {
            return new CampaignBusinessLocationResolution
            {
                IsResolved = true,
                Label = "Sandton",
                Area = "Sandton",
                City = "Sandton",
                Province = "Gauteng",
                Latitude = -26.1076,
                Longitude = 28.0567,
                Source = "test",
                Precision = "local"
            };
        }
    }

    private sealed class TestPlanningBudgetAllocationService : IPlanningBudgetAllocationService
    {
        public PlanningBudgetAllocation Resolve(CampaignPlanningRequest request)
        {
            return new PlanningBudgetAllocation
            {
                AudienceSegment = "premium",
                ChannelPolicyKey = "test-channel",
                GeoPolicyKey = "test-geo",
                ChannelAllocations = new List<PlanningChannelAllocation>
                {
                    new() { Channel = "ooh", Weight = 0.4m, Amount = request.SelectedBudget * 0.4m },
                    new() { Channel = "radio", Weight = 0.3m, Amount = request.SelectedBudget * 0.3m },
                    new() { Channel = "digital", Weight = 0.3m, Amount = request.SelectedBudget * 0.3m }
                },
                GeoAllocations = new List<PlanningGeoAllocation>
                {
                    new() { Bucket = "origin", Weight = 0.5m, Amount = request.SelectedBudget * 0.5m, RadiusKm = null },
                    new() { Bucket = "nearby", Weight = 0.25m, Amount = request.SelectedBudget * 0.25m, RadiusKm = 20 },
                    new() { Bucket = "wider", Weight = 0.25m, Amount = request.SelectedBudget * 0.25m, RadiusKm = null }
                }
            };
        }

        public PlanningBudgetAllocation RebalanceChannelTargets(CampaignPlanningRequest request, IReadOnlyDictionary<string, int> channelShares)
        {
            return Resolve(request);
        }
    }
}
