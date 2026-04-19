using Advertified.App.Campaigns;
using FluentAssertions;

namespace Advertified.App.Tests;

public sealed class RecommendationClientDocumentShaperTests
{
    [Fact]
    public void ShapeProposal_CollapsesMallPlacementsIntoSingleClientBlock()
    {
        var proposal = new RecommendationProposalDocumentModel
        {
            Label = "Proposal A",
            TotalCost = 30000m,
            Items = new[]
            {
                new RecommendationLineDocumentModel
                {
                    Channel = "OOH",
                    Title = "Rosebank Mall - Digital Screen",
                    TotalCost = 10000m,
                    Quantity = 1,
                    Region = "Johannesburg",
                    SelectionReasons = new[] { "High foot traffic" }
                },
                new RecommendationLineDocumentModel
                {
                    Channel = "OOH",
                    Title = "Rosebank Mall - Digital Screen",
                    TotalCost = 10000m,
                    Quantity = 1,
                    Region = "Johannesburg",
                    SelectionReasons = new[] { "Premium retail context" }
                },
                new RecommendationLineDocumentModel
                {
                    Channel = "OOH",
                    Title = "Rosebank Mall - Billboard",
                    TotalCost = 10000m,
                    Quantity = 1,
                    Region = "Johannesburg",
                    SelectionReasons = new[] { "Commuter reach" }
                }
            }
        };

        var shaped = RecommendationClientDocumentShaper.ShapeProposal(proposal);

        shaped.Items.Should().ContainSingle();
        shaped.Items[0].Title.Should().Be("3 billboards and digital screens at Rosebank Mall");
        shaped.Items[0].TotalCost.Should().Be(30000m);
        shaped.Items[0].SelectionReasons.Should().BeEquivalentTo(new[]
        {
            "High foot traffic",
            "Premium retail context",
            "Commuter reach"
        });
    }

    [Fact]
    public void ShapeProposal_CollapsesRepeatedRadioStationPlacementsIntoSchedule()
    {
        var proposal = new RecommendationProposalDocumentModel
        {
            Label = "Proposal B",
            TotalCost = 45000m,
            Items = new[]
            {
                new RecommendationLineDocumentModel
                {
                    Channel = "Radio",
                    Title = "Kaya 959 - Breakfast",
                    TotalCost = 15000m,
                    Quantity = 1,
                    Region = "Gauteng",
                    Language = "English",
                    ShowDaypart = "Breakfast",
                    SelectionReasons = new[] { "Premium audience" }
                },
                new RecommendationLineDocumentModel
                {
                    Channel = "Radio",
                    Title = "Kaya 959 - Drive",
                    TotalCost = 15000m,
                    Quantity = 1,
                    Region = "Gauteng",
                    Language = "English",
                    ShowDaypart = "Drive",
                    SelectionReasons = new[] { "Repeat frequency" }
                },
                new RecommendationLineDocumentModel
                {
                    Channel = "Radio",
                    Title = "Kaya 959 - Midday",
                    TotalCost = 15000m,
                    Quantity = 1,
                    Region = "Gauteng",
                    Language = "English",
                    ShowDaypart = "Midday",
                    SelectionReasons = new[] { "Always-on presence" }
                }
            }
        };

        var shaped = RecommendationClientDocumentShaper.ShapeProposal(proposal);

        shaped.Items.Should().ContainSingle();
        shaped.Items[0].Title.Should().Be("Kaya 959 radio schedule");
        shaped.Items[0].TotalCost.Should().Be(45000m);
        shaped.Items[0].SelectionReasons[0].Should().Be("Slots: Breakfast, Drive, Midday");
        shaped.Items[0].SelectionReasons.Should().Contain(new[]
        {
            "Premium audience",
            "Repeat frequency",
            "Always-on presence"
        });
    }
}
