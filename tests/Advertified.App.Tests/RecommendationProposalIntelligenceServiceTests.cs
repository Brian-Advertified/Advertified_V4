using Advertified.App.Campaigns;
using Advertified.App.Data.Entities;
using Advertified.App.Services;
using FluentAssertions;

namespace Advertified.App.Tests;

public sealed class RecommendationProposalIntelligenceServiceTests
{
    [Fact]
    public void Build_ReplacesGenericPlannerCopyWithGroundedProposalNarrative()
    {
        var service = new RecommendationProposalIntelligenceService();
        var request = new RecommendationProposalIntelligenceRequest(
            new RecommendationProposalCampaignContext(
                "Nandi Client",
                "Family Store",
                "Winter footfall campaign",
                "Growth",
                125000m,
                "Selected budget",
                "R 125,000.00",
                "Drive mall footfall in Johannesburg",
                null,
                new[] { "Johannesburg" },
                "Parents and grocery shoppers",
                new[] { "English" }),
            new CampaignRecommendation
            {
                RecommendationType = "hybrid:ooh_focus",
                Summary = "Recommended 2 planned item(s) across OOH, Radio. Budget split target: Radio 50% | Billboards and Digital Screens 50%.",
                Rationale = "Plan built within budget of 125,000, prioritising geography fit, audience fit, requested mix targets, and available inventory. Selected mix: OOH, Radio. Requested target: Radio 50% | Billboards and Digital Screens 50%. Strategy weighting favoured premium audience alignment. Budget allocation favored Billboards and Digital Screens (60%) and the local geo bucket (65%).",
                TotalCost = 120000m
            },
            new RecommendationOpportunityContextModel
            {
                ArchetypeName = "Active Scaler",
                DetectedGaps = new[] { "Not visible in key shopping routes", "Weak local demand capture" },
                ExpectedOutcome = "Expected impact: Stronger store visit signals during the campaign window."
            },
            new[]
            {
                new RecommendationLineDocumentModel
                {
                    Channel = "OOH",
                    Title = "Rosebank Mall digital screen",
                    TotalCost = 70000m,
                    Quantity = 2,
                    Region = "Rosebank, Johannesburg"
                },
                new RecommendationLineDocumentModel
                {
                    Channel = "Radio",
                    Title = "Kaya 959 breakfast schedule",
                    TotalCost = 50000m,
                    Quantity = 1,
                    Region = "Gauteng"
                }
            },
            1);

        var result = service.Build(request);

        result.Label.Should().Be("Proposal B - Multi-Channel Expansion (Best balance)");
        result.Strategy.Should().Contain("Billboards and Digital Screens");
        result.Summary.Should().Contain("Family Store");
        result.Summary.Should().Contain("Billboards and Digital Screens");
        result.Summary.Should().NotContain("planned item(s)");
        result.Summary.Should().NotContain("Budget split target");
        result.Rationale.Should().NotContain("Plan built within budget");
        result.Rationale.Should().NotContain("OOH");
        result.Rationale.Should().NotContain("Requested target");
        result.Rationale.Should().NotContain("Budget allocation");
        result.Narrative.ExpectedOutcome.Should().Be("Stronger store visit signals during the campaign window.");
        result.Narrative.ChannelRoles.Should().Contain(role => role.StartsWith("Billboards and Digital Screens:", StringComparison.Ordinal));
        result.Narrative.ChannelRoles.Should().Contain("Radio: keeps the campaign present during daily listening routines, helping the message move from recognition to recall.");
        result.Narrative.SuccessMeasures.Should().Contain(measure => measure.Contains("Supplier availability", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Build_PreservesSpecificReasoningWhenItIsAlreadyClientReady()
    {
        var service = new RecommendationProposalIntelligenceService();
        var request = new RecommendationProposalIntelligenceRequest(
            new RecommendationProposalCampaignContext(
                "Client",
                "Clinic Group",
                "Awareness campaign",
                "Scale",
                220000m,
                "Selected budget",
                "R 220,000.00",
                "Build awareness for a new branch",
                null,
                new[] { "Pretoria" },
                "Working professionals",
                Array.Empty<string>()),
            new CampaignRecommendation
            {
                RecommendationType = "hybrid:balanced",
                Summary = "A local launch plan built around Pretoria visibility and appointment demand.",
                Rationale = "This balances outdoor visibility with digital demand capture so the branch is visible before and during the launch period.",
                TotalCost = 210000m
            },
            null,
            new[]
            {
                new RecommendationLineDocumentModel
                {
                    Channel = "digital",
                    Title = "Search campaign",
                    TotalCost = 80000m,
                    Quantity = 1,
                    Region = "Pretoria"
                }
            },
            0);

        var result = service.Build(request);

        result.Summary.Should().Be("A local launch plan built around Pretoria visibility and appointment demand.");
        result.Rationale.Should().Be("This balances outdoor visibility with digital demand capture so the branch is visible before and during the launch period.");
        result.Narrative.ChannelRoles.Should().Contain(role => role.StartsWith("Digital:", StringComparison.Ordinal));
    }
}
