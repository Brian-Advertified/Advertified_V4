using Advertified.App.Configuration;
using Advertified.App.Contracts.Campaigns;
using Advertified.App.Domain.Campaigns;
using Advertified.App.Services;
using FluentAssertions;

namespace Advertified.App.Tests;

public sealed class RecommendationPlanBuilderSuburbTokenRadioTests
{
    [Fact]
    public void BuildPlan_WithTargetMix_AnchorsRadioToSuburbTokenWhenAvailable()
    {
        var policyService = new PlanningPolicyService(new PlanningPolicySnapshotProvider(new PlanningPolicyOptions
        {
            Scale = new PackagePlanningPolicy { BudgetFloor = 500000m, MinimumNationalRadioCandidates = 1, RequireNationalCapableRadio = true },
            Dominance = new PackagePlanningPolicy { BudgetFloor = 1000000m, MinimumNationalRadioCandidates = 2, RequireNationalCapableRadio = true }
        }));
        var builder = new RecommendationPlanBuilder(policyService);

        var request = new CampaignPlanningRequest
        {
            SelectedBudget = 100000m,
            GeographyScope = "local",
            Cities = new List<string> { "Johannesburg" },
            Suburbs = new List<string> { "DiepKloof, Soweto" },
            TargetRadioShare = 33,
            TargetOohShare = 33,
            TargetDigitalShare = 34
        };

        var candidates = new List<InventoryCandidate>
        {
            new()
            {
                SourceId = Guid.NewGuid(),
                SourceType = "ooh",
                DisplayName = "Any OOH",
                MediaType = "OOH",
                Cost = 42000m,
                IsAvailable = true,
                Score = 10m
            },
            new()
            {
                SourceId = Guid.NewGuid(),
                SourceType = "digital",
                DisplayName = "Any Digital",
                MediaType = "Digital",
                Cost = 35000m,
                IsAvailable = true,
                Score = 10m
            },
            new()
            {
                SourceId = Guid.NewGuid(),
                SourceType = "radio_slot",
                DisplayName = "Kaya 959 - spot",
                MediaType = "Radio",
                Cost = 18000m,
                IsAvailable = true,
                Score = 99m,
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
                Cost = 56000m,
                IsAvailable = true,
                Score = 1m,
                Metadata = new Dictionary<string, object?>
                {
                    ["cityLabels"] = new[] { "Johannesburg", "Soweto" }
                }
            }
        };

        var plan = builder.BuildPlan(candidates, request, diversify: true);
        plan.Should().Contain(item => item.MediaType == "Radio" && item.DisplayName.Contains("Jozi FM"));
    }
}
