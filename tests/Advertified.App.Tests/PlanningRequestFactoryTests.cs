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
        var factory = new PlanningRequestFactory(new TestGeocodingService());
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
    }

    private sealed class TestGeocodingService : IGeocodingService
    {
        public GeocodingResolution ResolveLocation(string? rawLocation) => new();

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
}
