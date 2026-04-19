using Advertified.App.Contracts.Campaigns;
using Advertified.App.Domain.Campaigns;
using Advertified.App.Services;
using FluentAssertions;

namespace Advertified.App.Tests;

public sealed class PlanningBudgetAllocationServiceTests
{
    [Fact]
    public void Resolve_UsesBudgetBandRules_AndAppliesTvFloorWhenPreferred()
    {
        var service = new PlanningBudgetAllocationService(new PlanningBudgetAllocationSnapshotProvider(
            new PlanningBudgetAllocationPolicySnapshot
            {
                BudgetBands = new[]
                {
                    new BudgetBandAllocationPolicyRule
                    {
                        Name = "100k-500k",
                        Min = 100000m,
                        Max = 500000m,
                        OohTarget = 0.42m,
                        BillboardShareOfOoh = 0.65m,
                        TvMin = 0.08m,
                        TvEligible = true,
                        RadioRange = new[] { 0.25m, 0.30m },
                        DigitalRange = new[] { 0.20m, 0.25m }
                    }
                },
                GlobalRules = new PlanningAllocationGlobalRules
                {
                    MaxOoh = 0.50m,
                    MinDigital = 0.15m,
                    EnforceTvFloorIfPreferred = true
                },
                GeoRules = new[]
                {
                    new GeoAllocationPolicyRule
                    {
                        PolicyKey = "geo_default",
                        Priority = 100,
                        GeographyScope = "national",
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
            SelectedBudget = 300000m,
            Objective = "awareness",
            GeographyScope = "national",
            PreferredMediaTypes = new List<string> { "ooh", "radio", "tv", "digital" }
        });

        allocation.ChannelPolicyKey.Should().Be("budget_band_100k_500k");
        allocation.ChannelAllocations.Should().ContainSingle(x => x.Channel == "billboard" && x.Weight == 0.273m);
        allocation.ChannelAllocations.Should().ContainSingle(x => x.Channel == "digital_screen" && x.Weight == 0.147m);
        allocation.ChannelAllocations.Should().ContainSingle(x => x.Channel == "tv" && x.Weight == 0.08m);
        allocation.ChannelAllocations.Should().ContainSingle(x => x.Channel == "radio" && x.Weight == 0.275m);
        allocation.ChannelAllocations.Should().ContainSingle(x => x.Channel == "digital" && x.Weight == 0.225m);
    }

    [Fact]
    public void Resolve_DoesNotForceTvWhenBandIsNotEligible()
    {
        var service = new PlanningBudgetAllocationService(new PlanningBudgetAllocationSnapshotProvider(
            new PlanningBudgetAllocationPolicySnapshot
            {
                BudgetBands = new[]
                {
                    new BudgetBandAllocationPolicyRule
                    {
                        Name = "20k-100k",
                        Min = 20000m,
                        Max = 100000m,
                        OohTarget = 0.45m,
                        BillboardShareOfOoh = 0.70m,
                        TvMin = 0m,
                        TvEligible = false,
                        RadioRange = new[] { 0.30m, 0.35m },
                        DigitalRange = new[] { 0.20m, 0.25m }
                    }
                },
                GlobalRules = new PlanningAllocationGlobalRules
                {
                    MaxOoh = 0.50m,
                    MinDigital = 0.15m,
                    EnforceTvFloorIfPreferred = true
                }
            }));

        var allocation = service.Resolve(new CampaignPlanningRequest
        {
            SelectedBudget = 50000m,
            Objective = "awareness",
            GeographyScope = "provincial",
            PreferredMediaTypes = new List<string> { "tv", "ooh", "radio", "digital" }
        });

        allocation.ChannelAllocations.Should().NotContain(x => x.Channel == "tv" && x.Weight > 0m);
        allocation.ChannelAllocations.Should().ContainSingle(x => x.Channel == "billboard" && x.Weight == 0.315m);
        allocation.ChannelAllocations.Should().ContainSingle(x => x.Channel == "digital_screen" && x.Weight == 0.135m);
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
        allocation.ChannelAllocations.Should().ContainSingle(x => x.Channel == "billboard" && x.Weight == 0.325m);
        allocation.ChannelAllocations.Should().ContainSingle(x => x.Channel == "digital_screen" && x.Weight == 0.175m);
        allocation.ChannelAllocations.Should().ContainSingle(x => x.Channel == "radio" && x.Weight == 0.3m);
        allocation.ChannelAllocations.Should().ContainSingle(x => x.Channel == "digital" && x.Weight == 0.2m);
        allocation.ChannelAllocations.Should().NotContain(x => x.Channel == "tv" && x.Weight > 0m);
    }
}
