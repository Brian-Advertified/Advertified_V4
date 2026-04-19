using Advertified.App.Configuration;
using Advertified.App.Contracts.Campaigns;
using Advertified.App.Domain.Campaigns;
using Advertified.App.Services;
using Advertified.App.Services.Abstractions;
using FluentAssertions;

namespace Advertified.App.Tests;

public sealed class PlanningAllocationPolicyTests
{
    [Fact]
    public void BuildRequestedMixLabel_UsesBudgetAllocationWhenExplicitSharesAreMissing()
    {
        var service = CreatePlanningPolicyService();

        var request = new CampaignPlanningRequest
        {
            SelectedBudget = 300000m,
            PreferredMediaTypes = new List<string> { "ooh", "radio", "tv", "digital" },
            BudgetAllocation = new PlanningBudgetAllocation
            {
                ChannelAllocations =
                {
                    new PlanningChannelAllocation { Channel = "billboard", Weight = 0.27m },
                    new PlanningChannelAllocation { Channel = "digital_screen", Weight = 0.15m },
                    new PlanningChannelAllocation { Channel = "radio", Weight = 0.28m },
                    new PlanningChannelAllocation { Channel = "digital", Weight = 0.22m },
                    new PlanningChannelAllocation { Channel = "tv", Weight = 0.08m },
                }
            }
        };

        service.BuildRequestedMixLabel(request).Should().Be("Radio 28% | Billboards 27% | Digital 22% | Digital Screens 15% | TV 8%");
        service.GetTargetShare("tv", request).Should().Be(8);
        service.GetRequiredChannels(request).Should().Contain(new[] { "billboard", "digital_screen", "radio", "digital", "tv" });
    }

    [Fact]
    public void PreferredMediaFallbackFlags_SkipTvWhenTvTargetIsZeroByDesign()
    {
        var policyService = CreatePlanningPolicyService();
        var explainabilityService = new RecommendationExplainabilityService(new StubPlanningScoreService(), policyService);

        var flags = explainabilityService.GetPreferredMediaFallbackFlags(
            new CampaignPlanningRequest
            {
                PreferredMediaTypes = new List<string> { "ooh", "radio", "tv", "digital" },
                BudgetAllocation = new PlanningBudgetAllocation
                {
                    ChannelAllocations =
                    {
                        new PlanningChannelAllocation { Channel = "billboard", Weight = 0.30m },
                        new PlanningChannelAllocation { Channel = "digital_screen", Weight = 0.15m },
                        new PlanningChannelAllocation { Channel = "radio", Weight = 0.32m },
                        new PlanningChannelAllocation { Channel = "digital", Weight = 0.23m }
                    }
                }
            },
            new List<PlannedItem>
            {
                new() { MediaType = "billboard" },
                new() { MediaType = "digital_screen" },
                new() { MediaType = "Radio" },
                new() { MediaType = "Digital" }
            },
            Array.Empty<InventoryCandidate>());

        flags.Should().BeEmpty();
    }

    [Fact]
    public void PreferredMediaFallbackFlags_EmitTvWhenTvWasTargetedButNotSelected()
    {
        var policyService = CreatePlanningPolicyService();
        var explainabilityService = new RecommendationExplainabilityService(new StubPlanningScoreService(), policyService);

        var flags = explainabilityService.GetPreferredMediaFallbackFlags(
            new CampaignPlanningRequest
            {
                PreferredMediaTypes = new List<string> { "ooh", "radio", "tv", "digital" },
                BudgetAllocation = new PlanningBudgetAllocation
                {
                    ChannelAllocations =
                    {
                        new PlanningChannelAllocation { Channel = "billboard", Weight = 0.27m },
                        new PlanningChannelAllocation { Channel = "digital_screen", Weight = 0.15m },
                        new PlanningChannelAllocation { Channel = "radio", Weight = 0.28m },
                        new PlanningChannelAllocation { Channel = "digital", Weight = 0.22m },
                        new PlanningChannelAllocation { Channel = "tv", Weight = 0.08m }
                    }
                }
            },
            new List<PlannedItem>
            {
                new() { MediaType = "billboard" },
                new() { MediaType = "digital_screen" },
                new() { MediaType = "Radio" },
                new() { MediaType = "Digital" }
            },
            Array.Empty<InventoryCandidate>());

        flags.Should().ContainSingle("preferred_media_unfulfilled:tv");
    }

    private static PlanningPolicyService CreatePlanningPolicyService()
    {
        return new PlanningPolicyService(new PlanningPolicySnapshotProvider(new PlanningPolicyOptions
        {
            Scale = new PackagePlanningPolicy
            {
                BudgetFloor = 500000m,
                MinimumNationalRadioCandidates = 1,
                RequireNationalCapableRadio = true,
                RequirePremiumNationalRadio = false,
                NationalRadioBonus = 10,
                NonNationalRadioPenalty = 8,
                RegionalRadioPenalty = 6
            },
            Dominance = new PackagePlanningPolicy
            {
                BudgetFloor = 1000000m,
                MinimumNationalRadioCandidates = 2,
                RequireNationalCapableRadio = true,
                RequirePremiumNationalRadio = true,
                NationalRadioBonus = 12,
                NonNationalRadioPenalty = 10,
                RegionalRadioPenalty = 8
            }
        }));
    }

    private sealed class StubPlanningScoreService : IPlanningScoreService
    {
        public PlanningCandidateAnalysis AnalyzeCandidate(InventoryCandidate candidate, CampaignPlanningRequest request) => new(0m, Array.Empty<string>(), Array.Empty<string>(), 0m);
        public decimal GeoScore(InventoryCandidate candidate, CampaignPlanningRequest request) => 0m;
        public decimal AudienceScore(InventoryCandidate candidate, CampaignPlanningRequest request) => 0m;
        public decimal MediaPreferenceScore(InventoryCandidate candidate, CampaignPlanningRequest request) => 0m;
        public decimal MixTargetScore(InventoryCandidate candidate, CampaignPlanningRequest request) => 0m;
        public decimal BudgetScore(InventoryCandidate candidate, CampaignPlanningRequest request) => 0m;
        public decimal IndustryContextFitScore(InventoryCandidate candidate, CampaignPlanningRequest request) => 0m;
    }
}
