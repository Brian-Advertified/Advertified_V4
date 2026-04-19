using Advertified.App.Campaigns;
using FluentAssertions;

namespace Advertified.App.Tests;

public sealed class RecommendationClientDocumentShaperTests
{
    [Fact]
    public void ShapeProposal_CollapsesIndoorMallPlacementsAndGroupsOutdoorStaticPlacementsSeparately()
    {
        var proposal = new RecommendationProposalDocumentModel
        {
            Label = "Proposal A",
            TotalCost = 40000m,
            Items = new[]
            {
                new RecommendationLineDocumentModel
                {
                    Channel = "OOH",
                    Title = "Rosebank Mall - Static Lift Door Wrap | Indoor",
                    TotalCost = 10000m,
                    Quantity = 1,
                    Region = "Johannesburg",
                    TrafficCount = "12,000",
                    VenueType = "premium_mall",
                    EnvironmentType = "mall_interior",
                    SelectionReasons = new[] { "High foot traffic" }
                },
                new RecommendationLineDocumentModel
                {
                    Channel = "OOH",
                    Title = "Rosebank Mall - Static Wall Banner | Indoor",
                    TotalCost = 10000m,
                    Quantity = 1,
                    Region = "Johannesburg",
                    TrafficCount = "18000",
                    VenueType = "premium_mall",
                    EnvironmentType = "mall_interior",
                    SelectionReasons = new[] { "Premium retail context" }
                },
                new RecommendationLineDocumentModel
                {
                    Channel = "OOH",
                    Title = "Rosebank Mall - Static Entrance Wall | Outdoor",
                    TotalCost = 10000m,
                    Quantity = 1,
                    Region = "Johannesburg",
                    SelectionReasons = new[] { "Commuter reach" }
                },
                new RecommendationLineDocumentModel
                {
                    Channel = "OOH",
                    Title = "Rosebank Mall - Static Parking Wall | Outdoor",
                    TotalCost = 10000m,
                    Quantity = 1,
                    Region = "Johannesburg",
                    SelectionReasons = new[] { "Arrival visibility" }
                }
            }
        };

        var shaped = RecommendationClientDocumentShaper.ShapeProposal(proposal);

        shaped.Items.Should().HaveCount(2);
        shaped.Items[0].Title.Should().Be("2 indoor mall placements at Rosebank Mall");
        shaped.Items[0].TotalCost.Should().Be(20000m);
        shaped.Items[0].TrafficCount.Should().Be("30000");
        shaped.Items[0].SelectionReasons.Should().BeEquivalentTo(new[]
        {
            "High foot traffic",
            "Premium retail context"
        });
        shaped.Items[1].Title.Should().Be("2 outdoor mall placements at Rosebank Mall");
        shaped.Items[1].TotalCost.Should().Be(20000m);
    }

    [Fact]
    public void ShapeProposal_CollapsesRepeatedIndoorMallRowsWhenMallMetadataExists()
    {
        var proposal = new RecommendationProposalDocumentModel
        {
            Label = "Proposal C",
            TotalCost = 30500m,
            Items = new[]
            {
                new RecommendationLineDocumentModel
                {
                    Channel = "OOH",
                    Title = "Bloed Street Mall,, Pretoria",
                    TotalCost = 4000m,
                    Quantity = 1,
                    Region = "Pretoria",
                    TrafficCount = "15000",
                    SlotType = "Static Lift Door Wrap | Indoor",
                    VenueType = "community_mall",
                    SelectionReasons = new[] { "CBD footfall" }
                },
                new RecommendationLineDocumentModel
                {
                    Channel = "OOH",
                    Title = "Bloed Street Mall,, Pretoria",
                    TotalCost = 10000m,
                    Quantity = 1,
                    Region = "Pretoria",
                    TrafficCount = "25000",
                    SlotType = "Static Wall Banner | Indoor",
                    VenueType = "community_mall",
                    SelectionReasons = new[] { "Taxi rank traffic" }
                },
                new RecommendationLineDocumentModel
                {
                    Channel = "OOH",
                    Title = "Sunnypark Shopping, Centre, Pretoria",
                    TotalCost = 8500m,
                    Quantity = 1,
                    Region = "Pretoria",
                    TrafficCount = "8,500",
                    SlotType = "Static Escalator Wrap | Indoor",
                    VenueType = "community_mall",
                    SelectionReasons = new[] { "Shopping audience" }
                },
                new RecommendationLineDocumentModel
                {
                    Channel = "OOH",
                    Title = "Sunnypark Shopping, Centre, Pretoria",
                    TotalCost = 8000m,
                    Quantity = 1,
                    Region = "Pretoria",
                    TrafficCount = "9500",
                    SlotType = "Static Lift Panels | Indoor",
                    VenueType = "community_mall",
                    SelectionReasons = new[] { "Commuter traffic" }
                }
            }
        };

        var shaped = RecommendationClientDocumentShaper.ShapeProposal(proposal);

        shaped.Items.Should().HaveCount(2);
        shaped.Items.Select(item => item.Title).Should().BeEquivalentTo(new[]
        {
            "2 indoor mall placements at Bloed Street Mall, Pretoria",
            "2 indoor mall placements at Sunnypark Shopping, Centre, Pretoria"
        });
        shaped.Items.Select(item => item.TrafficCount).Should().BeEquivalentTo(new[]
        {
            "40000",
            "18000"
        });
    }

    [Fact]
    public void ShapeProposal_CollapsesOutdoorMallPlacementsSeparately()
    {
        var proposal = new RecommendationProposalDocumentModel
        {
            Label = "Proposal D",
            TotalCost = 21000m,
            Items = new[]
            {
                new RecommendationLineDocumentModel
                {
                    Channel = "OOH",
                    Title = "Sunnypark Shopping Centre, Pretoria - Static Parking Wall | Outdoor",
                    TotalCost = 10500m,
                    Quantity = 1,
                    Region = "Pretoria",
                    VenueType = "community_mall",
                    SelectionReasons = new[] { "Parking traffic" }
                },
                new RecommendationLineDocumentModel
                {
                    Channel = "OOH",
                    Title = "Sunnypark Shopping Centre, Pretoria - Static Entrance Wall | Outdoor",
                    TotalCost = 10500m,
                    Quantity = 1,
                    Region = "Pretoria",
                    VenueType = "community_mall",
                    SelectionReasons = new[] { "Arrival visibility" }
                }
            }
        };

        var shaped = RecommendationClientDocumentShaper.ShapeProposal(proposal);

        shaped.Items.Should().HaveCount(1);
        shaped.Items[0].Title.Should().Be("2 outdoor mall placements at Sunnypark Shopping Centre, Pretoria");
        shaped.Items[0].TotalCost.Should().Be(21000m);
    }

    [Fact]
    public void ShapeProposal_KeepsMallScreensSeparateFromGroupedStaticPlacements()
    {
        var proposal = new RecommendationProposalDocumentModel
        {
            Label = "Proposal F",
            TotalCost = 31000m,
            Items = new[]
            {
                new RecommendationLineDocumentModel
                {
                    Channel = "billboard",
                    Title = "Sunnypark Shopping Centre, Pretoria",
                    SlotType = "Static Escalator Wrap | Indoor",
                    TotalCost = 9000m,
                    Quantity = 1,
                    Region = "Pretoria",
                    VenueType = "community_mall"
                },
                new RecommendationLineDocumentModel
                {
                    Channel = "billboard",
                    Title = "Sunnypark Shopping Centre, Pretoria",
                    SlotType = "Static Lift Panels | Indoor",
                    TotalCost = 9500m,
                    Quantity = 1,
                    Region = "Pretoria",
                    VenueType = "community_mall"
                },
                new RecommendationLineDocumentModel
                {
                    Channel = "digital_screen",
                    Title = "Sunnypark Shopping Centre, Pretoria",
                    SlotType = "Digital Screen | Outdoor",
                    TotalCost = 12500m,
                    Quantity = 1,
                    Region = "Pretoria",
                    VenueType = "community_mall"
                }
            }
        };

        var shaped = RecommendationClientDocumentShaper.ShapeProposal(proposal);

        shaped.Items.Should().HaveCount(2);
        shaped.Items[0].Title.Should().Be("2 indoor mall placements at Sunnypark Shopping Centre, Pretoria");
        shaped.Items[1].Title.Should().Be("Sunnypark Shopping Centre, Pretoria");
    }

    [Fact]
    public void ShapeProposal_GroupsSunnyparkLikeCurrentDevRecommendation()
    {
        var proposal = new RecommendationProposalDocumentModel
        {
            Label = "Proposal G",
            TotalCost = 139650m,
            Items = new[]
            {
                new RecommendationLineDocumentModel
                {
                    Channel = "billboard",
                    Title = "Sunnypark Shopping Centre, Pretoria",
                    SlotType = "Static Elevator Wrap | Indoor",
                    TotalCost = 19425m,
                    Quantity = 1,
                    Region = "Pretoria",
                    VenueType = "community_mall"
                },
                new RecommendationLineDocumentModel
                {
                    Channel = "billboard",
                    Title = "Sunnypark Shopping Centre, Pretoria",
                    SlotType = "Static Escalator Wrap | Indoor",
                    TotalCost = 8925m,
                    Quantity = 1,
                    Region = "Pretoria",
                    VenueType = "community_mall"
                },
                new RecommendationLineDocumentModel
                {
                    Channel = "billboard",
                    Title = "Sunnypark Shopping Centre, Pretoria",
                    SlotType = "Static Glass Balustrades | Indoor",
                    TotalCost = 5775m,
                    Quantity = 1,
                    Region = "Pretoria",
                    VenueType = "community_mall"
                },
                new RecommendationLineDocumentModel
                {
                    Channel = "billboard",
                    Title = "Sunnypark Shopping Centre, Pretoria",
                    SlotType = "Static Hanging Banner | Indoor",
                    TotalCost = 5250m,
                    Quantity = 1,
                    Region = "Pretoria",
                    VenueType = "community_mall"
                },
                new RecommendationLineDocumentModel
                {
                    Channel = "billboard",
                    Title = "Sunnypark Shopping Centre, Pretoria",
                    SlotType = "Static Lift Door Wrap | Indoor",
                    TotalCost = 5775m,
                    Quantity = 1,
                    Region = "Pretoria",
                    VenueType = "community_mall"
                },
                new RecommendationLineDocumentModel
                {
                    Channel = "billboard",
                    Title = "Sunnypark Shopping Centre, Pretoria",
                    SlotType = "Static Lift Panels | Indoor",
                    TotalCost = 8925m,
                    Quantity = 1,
                    Region = "Pretoria",
                    VenueType = "community_mall"
                },
                new RecommendationLineDocumentModel
                {
                    Channel = "billboard",
                    Title = "Sunnypark Shopping Centre, Pretoria",
                    SlotType = "Static Overhead Banner | Indoor",
                    TotalCost = 4725m,
                    Quantity = 1,
                    Region = "Pretoria",
                    VenueType = "community_mall"
                },
                new RecommendationLineDocumentModel
                {
                    Channel = "billboard",
                    Title = "Sunnypark Shopping Centre, Pretoria",
                    SlotType = "Static Pillar Wrap | Indoor",
                    TotalCost = 4725m,
                    Quantity = 1,
                    Region = "Pretoria",
                    VenueType = "community_mall"
                },
                new RecommendationLineDocumentModel
                {
                    Channel = "billboard",
                    Title = "Sunnypark Shopping Centre, Pretoria",
                    SlotType = "Static Wall Banner | Indoor",
                    TotalCost = 4725m,
                    Quantity = 1,
                    Region = "Pretoria",
                    VenueType = "community_mall"
                },
                new RecommendationLineDocumentModel
                {
                    Channel = "billboard",
                    Title = "Sunnypark Shopping Centre, Pretoria",
                    SlotType = "Static Entrance Wall | Outdoor",
                    TotalCost = 44100m,
                    Quantity = 1,
                    Region = "Pretoria",
                    VenueType = "community_mall"
                },
                new RecommendationLineDocumentModel
                {
                    Channel = "billboard",
                    Title = "Sunnypark Shopping Centre, Pretoria",
                    SlotType = "Static Parking Gantry | Outdoor",
                    TotalCost = 5250m,
                    Quantity = 1,
                    Region = "Pretoria",
                    VenueType = "community_mall"
                },
                new RecommendationLineDocumentModel
                {
                    Channel = "billboard",
                    Title = "Sunnypark Shopping Centre, Pretoria",
                    SlotType = "Static Parking Wall | Outdoor",
                    TotalCost = 6300m,
                    Quantity = 1,
                    Region = "Pretoria",
                    VenueType = "community_mall"
                },
                new RecommendationLineDocumentModel
                {
                    Channel = "billboard",
                    Title = "Sunnypark Shopping Centre, Pretoria",
                    SlotType = "Static Parking Wall | Outdoor",
                    TotalCost = 5250m,
                    Quantity = 1,
                    Region = "Pretoria",
                    VenueType = "community_mall"
                },
                new RecommendationLineDocumentModel
                {
                    Channel = "digital_screen",
                    Title = "Sunnypark Shopping Centre, Pretoria",
                    SlotType = "Digital Screen | Outdoor",
                    TotalCost = 10500m,
                    Quantity = 1,
                    Region = "Pretoria",
                    VenueType = "community_mall"
                }
            }
        };

        var shaped = RecommendationClientDocumentShaper.ShapeProposal(proposal);

        shaped.Items.Should().HaveCount(3);
        shaped.Items.Select(item => item.Title).Should().Equal(
            "9 indoor mall placements at Sunnypark Shopping Centre, Pretoria",
            "4 outdoor mall placements at Sunnypark Shopping Centre, Pretoria",
            "Sunnypark Shopping Centre, Pretoria");
        shaped.Items.Select(item => item.TotalCost).Should().Equal(
            68250m,
            60900m,
            10500m);
    }

    [Fact]
    public void ShapeProposal_DoesNotCollapseNonMallScreens()
    {
        var proposal = new RecommendationProposalDocumentModel
        {
            Label = "Proposal E",
            TotalCost = 28400m,
            Items = new[]
            {
                new RecommendationLineDocumentModel
                {
                    Channel = "OOH",
                    Title = "89 Grayston Drive - Digital Screen",
                    TotalCost = 14200m,
                    Quantity = 1,
                    Region = "Sandton, Gauteng",
                    EnvironmentType = "roadside",
                    SelectionReasons = new[] { "Commuter route" }
                },
                new RecommendationLineDocumentModel
                {
                    Channel = "OOH",
                    Title = "89 Grayston Drive - Digital Screen",
                    TotalCost = 14200m,
                    Quantity = 1,
                    Region = "Sandton, Gauteng",
                    EnvironmentType = "roadside",
                    SelectionReasons = new[] { "High visibility" }
                }
            }
        };

        var shaped = RecommendationClientDocumentShaper.ShapeProposal(proposal);

        shaped.Items.Should().HaveCount(2);
        shaped.Items.Select(item => item.Title).Should().Equal(
            "89 Grayston Drive - Digital Screen",
            "89 Grayston Drive - Digital Screen");
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
