using Advertified.App.Services;
using FluentAssertions;

namespace Advertified.App.Tests;

public sealed class LeadStrategyEngineTests
{
    [Fact]
    public void Build_PrioritizesPreferredChannels_FromIndustryPolicy()
    {
        var engine = new LeadStrategyEngine();
        var result = engine.Build(
            new LeadBusinessProfile
            {
                BusinessType = "Funeral Services",
                PrimaryLocation = "Johannesburg"
            },
            new LeadIndustryPolicyProfile
            {
                Key = "funeral_services",
                Name = "Funeral Services",
                PreferredChannels = new[] { "Search", "Radio", "OOH" }
            },
            null,
            new LeadOpportunityProfile
            {
                Key = "invisible_local_business",
                Name = "Invisible Local Business"
            },
            Array.Empty<LeadChannelDetectionResult>());

        result.Channels.Should().HaveCount(3);
        result.Channels[0].Channel.Should().Be("Digital");
        result.Channels[0].BudgetSharePercent.Should().BeGreaterThan(result.Channels[1].BudgetSharePercent);
        result.Channels[1].Channel.Should().Be("Radio");
        result.Channels[2].Channel.Should().Be("OOH");
    }

    [Fact]
    public void Build_UsesChannelDetection_AsSecondaryAdjustment()
    {
        var engine = new LeadStrategyEngine();
        var result = engine.Build(
            new LeadBusinessProfile
            {
                BusinessType = "Retail",
                PrimaryLocation = "Pretoria"
            },
            new LeadIndustryPolicyProfile
            {
                Key = "retail",
                Name = "Retail",
                PreferredChannels = new[] { "OOH", "Radio", "Digital" }
            },
            null,
            new LeadOpportunityProfile
            {
                Key = "promo_dependent_retailer",
                Name = "Promo-dependent retailer"
            },
            new[]
            {
                new LeadChannelDetectionResult
                {
                    Channel = "search",
                    Score = 95
                },
                new LeadChannelDetectionResult
                {
                    Channel = "billboards_ooh",
                    Score = 30
                }
            });

        result.Channels.Should().ContainSingle(channel => channel.Channel == "Digital")
            .Which.BudgetSharePercent.Should().BeGreaterThan(20);
        result.Channels.Should().ContainSingle(channel => channel.Channel == "OOH")
            .Which.BudgetSharePercent.Should().BeGreaterThan(25);
    }
}
