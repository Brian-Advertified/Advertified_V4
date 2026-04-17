using Advertified.App.Contracts.Campaigns;
using Advertified.App.Domain.Campaigns;
using Advertified.App.Services;
using FluentAssertions;

namespace Advertified.App.Tests;

public sealed class PlanningBudgetAllocationServiceTests
{
    [Fact]
    public void Resolve_UsesMatchingPolicyRules_ForPremiumProvincialAwareness()
    {
        var service = new PlanningBudgetAllocationService(new PlanningBudgetAllocationSnapshotProvider(
            new PlanningBudgetAllocationPolicySnapshot
            {
                ChannelRules = new[]
                {
                    new ChannelAllocationPolicyRule
                    {
                        PolicyKey = "premium-awareness-sub50k",
                        Priority = 100,
                        Objective = "awareness",
                        AudienceSegment = "premium",
                        MinBudget = 0,
                        MaxBudget = 49999.99m,
                        Weights = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
                        {
                            ["ooh"] = 0.35m,
                            ["radio"] = 0.25m,
                            ["digital"] = 0.40m
                        }
                    }
                },
                GeoRules = new[]
                {
                    new GeoAllocationPolicyRule
                    {
                        PolicyKey = "premium-provincial-awareness",
                        Priority = 100,
                        Objective = "awareness",
                        AudienceSegment = "premium",
                        GeographyScope = "provincial",
                        NearbyRadiusKm = 20,
                        Weights = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
                        {
                            ["origin"] = 0.50m,
                            ["nearby"] = 0.25m,
                            ["wider"] = 0.25m
                        }
                    }
                }
            }));

        var allocation = service.Resolve(new CampaignPlanningRequest
        {
            SelectedBudget = 40000m,
            Objective = "awareness",
            GeographyScope = "provincial",
            PricePositioning = "premium"
        });

        allocation.ChannelPolicyKey.Should().Be("premium-awareness-sub50k");
        allocation.GeoPolicyKey.Should().Be("premium-provincial-awareness");
        allocation.ChannelAllocations.Should().ContainSingle(x => x.Channel == "digital" && x.Weight == 0.40m);
        allocation.GeoAllocations.Should().ContainSingle(x => x.Bucket == "origin" && x.Weight == 0.50m);
    }

    [Fact]
    public void Resolve_RespectsExplicitTargetShares()
    {
        var service = new PlanningBudgetAllocationService(new PlanningBudgetAllocationSnapshotProvider(
            new PlanningBudgetAllocationPolicySnapshot()));

        var allocation = service.Resolve(new CampaignPlanningRequest
        {
            SelectedBudget = 100000m,
            Objective = "awareness",
            GeographyScope = "provincial",
            TargetOohShare = 50,
            TargetRadioShare = 30,
            TargetDigitalShare = 20
        });

        allocation.ChannelPolicyKey.Should().Be("explicit_request");
        allocation.ChannelAllocations.Should().ContainSingle(x => x.Channel == "ooh" && x.Weight == 0.5m);
        allocation.ChannelAllocations.Should().ContainSingle(x => x.Channel == "radio" && x.Weight == 0.3m);
        allocation.ChannelAllocations.Should().ContainSingle(x => x.Channel == "digital" && x.Weight == 0.2m);
    }
}
