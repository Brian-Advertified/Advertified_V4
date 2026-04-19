using Advertified.App.Configuration;
using Advertified.App.Contracts.Campaigns;
using Advertified.App.Domain.Campaigns;
using Advertified.App.Services;
using FluentAssertions;

namespace Advertified.App.Tests;

public sealed class RecommendationPlanBuilderChannelCapTests
{
    [Fact]
    public void BuildPlan_WithTargetMix_DoesNotLetRadioAndTvDominateAfterTargetsAreMet()
    {
        var policyService = new PlanningPolicyService(new PlanningPolicySnapshotProvider(new PlanningPolicyOptions
        {
            Scale = new PackagePlanningPolicy { BudgetFloor = 500000m, MinimumNationalRadioCandidates = 1, RequireNationalCapableRadio = true },
            Dominance = new PackagePlanningPolicy { BudgetFloor = 1000000m, MinimumNationalRadioCandidates = 2, RequireNationalCapableRadio = true }
        }));
        var builder = new RecommendationPlanBuilder(policyService, new TestBroadcastMasterDataService());

        var request = new CampaignPlanningRequest
        {
            SelectedBudget = 1000000m,
            GeographyScope = "national",
            TargetOohShare = 60,
            TargetRadioShare = 20,
            TargetTvShare = 15,
            TargetDigitalShare = 5
        };

        var candidates = new List<InventoryCandidate>
        {
            CreateCandidate("billboard", 100000m, 95m),
            CreateCandidate("billboard", 100000m, 94m),
            CreateCandidate("billboard", 100000m, 93m),
            CreateCandidate("billboard", 100000m, 92m),
            CreateCandidate("digital_screen", 100000m, 91m),
            CreateCandidate("digital_screen", 100000m, 90m),
            CreateCandidate("Radio", 180000m, 99m),
            CreateCandidate("Radio", 180000m, 98m),
            CreateCandidate("TV", 150000m, 97m),
            CreateCandidate("TV", 150000m, 96m),
            CreateCandidate("Digital", 50000m, 89m),
            CreateCandidate("Digital", 50000m, 88m)
        };

        var plan = builder.BuildPlan(candidates, request, diversify: false);

        var oohSpend = plan.Where(item => item.MediaType is "billboard" or "digital_screen").Sum(item => item.TotalCost);
        var radioSpend = plan.Where(item => item.MediaType == "Radio").Sum(item => item.TotalCost);
        var tvSpend = plan.Where(item => item.MediaType == "TV").Sum(item => item.TotalCost);
        var digitalSpend = plan.Where(item => item.MediaType == "Digital").Sum(item => item.TotalCost);

        oohSpend.Should().BeGreaterThanOrEqualTo(600000m);
        radioSpend.Should().BeLessThanOrEqualTo(220000m);
        tvSpend.Should().BeLessThanOrEqualTo(165000m);
        digitalSpend.Should().BeLessThanOrEqualTo(55000m);
    }

    private static InventoryCandidate CreateCandidate(string mediaType, decimal cost, decimal score)
    {
        return new InventoryCandidate
        {
            SourceId = Guid.NewGuid(),
            SourceType = "test",
            DisplayName = $"{mediaType}-{cost}",
            MediaType = mediaType,
            Cost = cost,
            IsAvailable = true,
            Score = score
        };
    }
}
