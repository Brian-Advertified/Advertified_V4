using System.Globalization;
using System.Linq;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Advertified.App.Campaigns;

internal static class RecommendationPdfGenerator
{
    private const string TermsUrl = "https://advertified.com/terms-of-service";
    private const string ColorGreen = "#1D9E75";
    private const string ColorGreenLight = "#E1F5EE";
    private const string ColorGreenDark = "#0F6E56";
    private const string ColorAmber = "#BA7517";
    private const string ColorAmberLight = "#FAEEDA";
    private const string ColorBlue = "#378ADD";
    private const string ColorInk = "#1A1A1A";
    private const string ColorMuted = "#6B6B6B";
    private const string ColorBorder = "#E4E4E0";
    private const string ColorSurface = "#F8F7F4";
    private const string ColorWhite = "#FFFFFF";

    internal static byte[] Generate(RecommendationDocumentModel model, string? logoPath)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        return Document.Create(document =>
        {
            document.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(34);
                page.DefaultTextStyle(text => text.FontSize(10).FontColor(ColorInk).FontFamily("Arial").LineHeight(1.4f));

                page.Header().Element(container => ComposeHeader(container, model, logoPath));
                page.Content().PaddingTop(14).Column(column =>
                {
                    column.Spacing(18);

                    column.Item().Element(container => ComposeTitleBlock(container, model));
                    if (model.OpportunityContext?.IsLeadOutreach == true)
                    {
                        column.Item().Element(container => ComposeLeadOutreachSummary(container, model));
                    }

                    column.Item().Element(container => ComposeCampaignOverview(container, model));

                    if (model.Proposals.Count > 0)
                    {
                        column.Item().Element(container => ComposeProposalComparison(container, model));
                        column.Item().PageBreak();
                    }

                    for (var index = 0; index < model.Proposals.Count; index++)
                    {
                        var proposal = model.Proposals[index];
                        if (index > 0)
                        {
                            column.Item().PageBreak();
                        }

                        column.Item().Element(container => ComposeProposalSection(container, model, proposal, index, model.Proposals.Count));
                    }

                    column.Item().PageBreak();
                    column.Item().Element(ComposeTermsSummary);
                });
                page.Footer().PaddingTop(10).Element(container => ComposeFooter(container, model));
            });
        }).GeneratePdf();
    }

    private static void ComposeHeader(IContainer container, RecommendationDocumentModel model, string? logoPath)
    {
        container.BorderBottom(2).BorderColor(ColorGreen).PaddingBottom(18).Row(row =>
        {
            row.RelativeItem().Column(col =>
            {
                if (!string.IsNullOrWhiteSpace(logoPath) && File.Exists(logoPath))
                {
                    col.Item().MaxHeight(24).Image(logoPath);
                }
                else
                {
                    col.Item().Text(text =>
                    {
                        text.Span("Advert").FontSize(18).SemiBold();
                        text.Span("ified").FontSize(18).SemiBold().FontColor(ColorGreen);
                    });
                }
            });

            row.ConstantItem(220).AlignRight().Column(col =>
            {
                col.Item().AlignRight().Background(ColorGreenLight).PaddingVertical(4).PaddingHorizontal(10).Text("Client Copy").FontSize(9).SemiBold().FontColor(ColorGreen);
                col.Item().PaddingTop(8).Text($"Generated: {model.GeneratedAtUtc:dd MMM yyyy}").FontSize(9).FontColor(ColorMuted);
                col.Item().Text($"{model.PackageName} | {model.BudgetDisplayText}").FontSize(9).FontColor(ColorMuted);
            });
        });
    }

    private static void ComposeTitleBlock(IContainer container, RecommendationDocumentModel model)
    {
        container.Column(column =>
        {
            column.Spacing(6);
            column.Item().Text(model.OpportunityContext?.IsLeadOutreach == true ? "Growth opportunity pack" : "Recommendation pack")
                .FontSize(9).SemiBold().FontColor(ColorMuted);
            column.Item().Text(model.CampaignName).FontSize(28).SemiBold();
            column.Item().Text(
                    !string.IsNullOrWhiteSpace(model.CampaignObjective)
                        ? $"Prepared for {RecommendationPdfCopy.ResolveBusinessReference(model)} - {RecommendationPdfCopy.ToClientCopy(model.CampaignObjective)}."
                        : $"Prepared for {RecommendationPdfCopy.ResolveBusinessReference(model)}.")
                .FontSize(11)
                .FontColor(ColorMuted);
        });
    }

    private static void ComposeLeadOutreachSummary(IContainer container, RecommendationDocumentModel model)
    {
        container.Border(1).BorderColor("#C7E0D6").Background("#F3FBF7").Padding(14).Column(column =>
        {
            column.Spacing(6);
            column.Item().Text("Growth opportunity snapshot").FontSize(11).SemiBold();
            column.Item().Text($"We found where {RecommendationPdfCopy.ResolveBusinessReference(model)} is losing customers, and built the campaign to fix it.").FontColor(ColorMuted);

            var topGaps = model.OpportunityContext!.DetectedGaps
                .Where(static gap => !string.IsNullOrWhiteSpace(gap))
                .Take(3)
                .Select(gap => RecommendationPdfCopy.TruncateClientCopy(gap, 150))
                .ToArray();
            foreach (var gap in topGaps)
            {
                column.Item().Text($"- {gap}").FontColor(ColorMuted);
            }

            if (!string.IsNullOrWhiteSpace(model.OpportunityContext.ExpectedOutcome))
            {
                column.Item().Text(RecommendationPdfCopy.TruncateClientCopy(model.OpportunityContext.ExpectedOutcome, 220)).FontColor(ColorMuted);
            }
        });
    }

    private static void ComposeCampaignOverview(IContainer container, RecommendationDocumentModel model)
    {
        var overviewItems = new List<(string Label, string Value)>
        {
            ("Client", RecommendationPdfCopy.ResolveBusinessReference(model)),
            ("Objective", RecommendationPdfCopy.ToClientCopy(model.CampaignObjective) is { Length: > 0 } objective ? objective : "Campaign growth"),
            ("Region", model.TargetAreas.Count > 0 ? string.Join(", ", model.TargetAreas.Select(RecommendationPdfCopy.ToClientCopy)) : "South Africa"),
            ("Target audience", !string.IsNullOrWhiteSpace(model.TargetAudienceSummary) ? RecommendationPdfCopy.ToClientCopy(model.TargetAudienceSummary) : "Audience not specified")
        };

        container.Column(column =>
        {
            column.Spacing(10);
            column.Item().Text("Campaign overview").FontSize(9).SemiBold().FontColor(ColorMuted);
            column.Item().Border(1).BorderColor(ColorBorder).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                });

                foreach (var item in overviewItems)
                {
                    table.Cell().BorderBottom(1).BorderColor(ColorBorder).Background(ColorSurface).Padding(12).Column(cell =>
                    {
                        cell.Spacing(4);
                        cell.Item().Text(item.Label).FontSize(8).SemiBold().FontColor(ColorMuted);
                        cell.Item().Text(item.Value).FontSize(10);
                    });
                }
            });
        });
    }

    private static void ComposeProposalComparison(IContainer container, RecommendationDocumentModel model)
    {
        container.Column(column =>
        {
            column.Spacing(12);
            column.Item().Text("Choose your growth path").FontSize(9).SemiBold().FontColor(ColorMuted);
            column.Item().Row(row =>
            {
                row.Spacing(10);
                for (var index = 0; index < model.Proposals.Count; index++)
                {
                    var proposal = model.Proposals[index];
                    row.RelativeItem().Element(item => ComposeProposalCard(item, proposal, index == GetFeaturedProposalIndex(model.Proposals.Count)));
                }
            });
        });
    }

    private static void ComposeProposalCard(IContainer container, RecommendationProposalDocumentModel proposal, bool featured)
    {
        var mediaCounts = RecommendationPdfPresentationBuilder.BuildProposalMediaCounts(proposal);

        container.Border(1.5f).BorderColor(featured ? ColorGreen : ColorBorder).Background(ColorWhite).Column(column =>
        {
            if (featured)
            {
                column.Item().AlignRight().Background(ColorAmber).PaddingVertical(3).PaddingHorizontal(8).Text("Recommended").FontSize(7).FontColor(ColorWhite).SemiBold();
            }

            column.Item().Background(featured ? ColorGreen : ColorSurface).Padding(14).Column(header =>
            {
                header.Spacing(3);
                header.Item().Text(proposal.Label).FontSize(8).SemiBold().FontColor(featured ? ColorWhite : ColorMuted);
                header.Item().Text(RecommendationPdfCopy.ToClientCopy(proposal.Strategy ?? "Recommendation option")).FontSize(11).SemiBold().FontColor(featured ? ColorWhite : ColorInk);
            });

            column.Item().Padding(14).Column(body =>
            {
                body.Spacing(8);
                body.Item().Text("Campaign total").FontSize(8).SemiBold().FontColor(ColorMuted);
                body.Item().Text(RecommendationPdfCopy.FormatCurrency(proposal.TotalCost)).FontSize(18).SemiBold();

                foreach (var count in mediaCounts)
                {
                    body.Item().Row(row =>
                    {
                        row.RelativeItem().Text(count.Channel).FontSize(9).FontColor(ColorMuted);
                        row.ConstantItem(40).AlignRight().Text(count.Quantity.ToString(CultureInfo.InvariantCulture)).FontSize(9).SemiBold();
                    });
                    body.Item().LineHorizontal(1).LineColor(ColorBorder);
                }

                body.Item().Row(row =>
                {
                    row.RelativeItem().Text("Placements").FontSize(9).FontColor(ColorMuted);
                    row.ConstantItem(60).AlignRight().Text($"{proposal.Items.Count} total").FontSize(9).SemiBold();
                });
            });

            if (!string.IsNullOrWhiteSpace(proposal.AcceptUrl))
            {
                column.Item().PaddingHorizontal(14).PaddingBottom(14).Hyperlink(proposal.AcceptUrl).Background(featured ? ColorGreen : ColorWhite).Border(1).BorderColor(ColorGreen).PaddingVertical(8).AlignCenter().Text($"Accept {proposal.Label}")
                    .FontSize(9)
                    .SemiBold()
                    .FontColor(featured ? ColorWhite : ColorGreen);
            }
        });
    }

    private static void ComposeProposalSection(IContainer container, RecommendationDocumentModel model, RecommendationProposalDocumentModel proposal, int proposalIndex, int totalProposals)
    {
        container.Column(section =>
        {
            section.Spacing(12);

            section.Item().Row(row =>
            {
                row.RelativeItem().Column(col =>
                {
                    col.Spacing(3);
                    col.Item().Text(proposal.Label).FontSize(10).SemiBold().FontColor(ColorMuted);
                    col.Item().Text(RecommendationPdfCopy.ToClientCopy(proposal.Strategy ?? "Recommendation option")).FontSize(18).SemiBold();
                    col.Item().Text(RecommendationPdfPresentationBuilder.BuildProposalSubheading(model, proposal)).FontSize(10).FontColor(ColorMuted);
                });
                row.ConstantItem(130).AlignRight().Text(RecommendationPdfCopy.FormatCurrency(proposal.TotalCost)).FontSize(18).SemiBold().FontColor(ColorGreen);
            });

            section.Item().Background(ColorGreenLight).Padding(12).BorderLeft(3).BorderColor(ColorGreen).Text(RecommendationPdfCopy.ToClientCopy(!string.IsNullOrWhiteSpace(proposal.Rationale) ? proposal.Rationale : proposal.Summary))
                .FontSize(10)
                .FontColor(ColorGreenDark);

            section.Item().Element(item => ComposeBudgetSplit(item, proposal));

            var groupedPlacements = RecommendationPdfPresentationBuilder.BuildPlacementSections(proposal);
            if (groupedPlacements.Count > 0)
            {
                section.Item().Text("Recommended placements").FontSize(9).SemiBold().FontColor(ColorMuted);
                foreach (var placementGroup in groupedPlacements)
                {
                    section.Item().Element(item => ComposePlacementSection(item, model, placementGroup));
                }
            }

            if (!string.IsNullOrWhiteSpace(proposal.AcceptUrl))
            {
                section.Item().Hyperlink(proposal.AcceptUrl).Background(ColorInk).Padding(16).Row(row =>
                {
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text($"Accept {proposal.Label}").FontSize(12).SemiBold().FontColor(ColorWhite);
                        col.Item().Text($"{RecommendationPdfCopy.FormatCurrency(proposal.TotalCost)} | Buy now, pay later available").FontSize(9).FontColor("#D1D5DB");
                    });
                    row.ConstantItem(140).AlignRight().Background(ColorGreen).PaddingVertical(10).PaddingHorizontal(12).AlignCenter().Text("Accept this proposal")
                        .FontSize(9)
                        .SemiBold()
                        .FontColor(ColorWhite);
                });
            }

            if (proposalIndex < totalProposals - 1)
            {
                section.Item().PaddingTop(8).LineHorizontal(1).LineColor(ColorBorder);
            }
        });
    }

    private static void ComposeBudgetSplit(IContainer container, RecommendationProposalDocumentModel proposal)
    {
        var split = RecommendationPdfPresentationBuilder.BuildChannelSpendSplit(proposal);
        if (split.Count == 0)
        {
            return;
        }

        container.Background(ColorSurface).Padding(14).Column(column =>
        {
            column.Spacing(8);
            column.Item().Text("Budget split").FontSize(8).SemiBold().FontColor(ColorMuted);
            foreach (var entry in split)
            {
                column.Item().Row(row =>
                {
                    row.ConstantItem(120).Text(entry.Label).FontSize(9).FontColor(ColorMuted);
                    row.ConstantItem(150).PaddingTop(4).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.ConstantColumn(Math.Max(8, (float)entry.Percent * 1.5f));
                            columns.RelativeColumn();
                        });

                        table.Cell().Height(8).Background(ResolveSpendColor(entry.Key));
                        table.Cell().Height(8).Background(ColorBorder);
                    });
                    row.ConstantItem(34).AlignRight().Text($"{entry.Percent}%").FontSize(9).SemiBold();
                });
            }
        });
    }

    private static void ComposePlacementCard(IContainer container, RecommendationDocumentModel model, RecommendationLineDocumentModel item)
    {
        var details = RecommendationPdfPresentationBuilder.BuildPlacementTags(item);
        var clientSummary = RecommendationPdfPresentationBuilder.BuildClientSelectionSummary(model, item);
        var badge = BuildChannelBadge(item.Channel);

        container.Border(1).BorderColor(ColorBorder).Background(ColorSurface).Padding(12).Row(row =>
        {
            row.Spacing(10);
            row.ConstantItem(34).Background(badge.Background).AlignMiddle().AlignCenter().PaddingVertical(8).Text(badge.Text).FontSize(8).SemiBold();
            row.RelativeItem().Column(col =>
            {
                col.Spacing(4);
                col.Item().Text(RecommendationPdfCopy.ToClientCopy(item.Title)).FontSize(11).SemiBold();
                col.Item().Text(RecommendationPdfPresentationBuilder.BuildPlacementLocation(item)).FontSize(9).FontColor(ColorMuted);
                if (details.Count > 0)
                {
                    col.Item().Row(tagRow =>
                    {
                        tagRow.Spacing(4);
                        foreach (var tag in details.Take(2))
                        {
                            tagRow.AutoItem().Border(1).BorderColor(ColorBorder).Background(ColorWhite).PaddingVertical(2).PaddingHorizontal(6).Text(tag).FontSize(7).FontColor(ColorMuted);
                        }
                    });
                }

                foreach (var line in clientSummary.Take(1))
                {
                    col.Item().Text(line).FontSize(8).FontColor(ColorMuted);
                }
            });
        });
    }

    private static void ComposePlacementSection(IContainer container, RecommendationDocumentModel model, PlacementSectionDocumentModel sectionModel)
    {
        var placementCount = sectionModel.Placements.Sum(item => Math.Max(1, item.Quantity));

        container.Border(1).BorderColor(ColorBorder).Background(ColorWhite).Padding(12).Column(section =>
        {
            section.Spacing(10);
            section.Item().Row(row =>
            {
                row.RelativeItem().Column(col =>
                {
                    col.Spacing(2);
                    col.Item().Text(sectionModel.Label).FontSize(10).SemiBold();
                    col.Item().Text($"{placementCount} placement{(placementCount == 1 ? string.Empty : "s")}").FontSize(8).FontColor(ColorMuted);
                });
                row.ConstantItem(120).AlignRight().Text(sectionModel.TotalLabel).FontSize(10).SemiBold().FontColor(ColorGreen);
            });

            foreach (var placement in sectionModel.Placements)
            {
                section.Item().Element(card => ComposePlacementCard(card, model, placement));
            }
        });
    }

    private static void ComposeTermsSummary(IContainer container)
    {
        container.Column(terms =>
        {
            terms.Spacing(10);
            terms.Item().Text("Terms and conditions summary").FontSize(9).SemiBold().FontColor(ColorMuted);
            terms.Item().Border(1).BorderColor(ColorBorder).Padding(14).Column(body =>
            {
                body.Spacing(6);
                foreach (var clause in BuildTermsSummary())
                {
                    body.Item().Text(clause).FontSize(9).FontColor(ColorMuted);
                }

                body.Item().PaddingTop(4).Text($"Full terms: {TermsUrl}").FontSize(9).SemiBold().FontColor(ColorGreen);
            });
        });
    }

    private static void ComposeFooter(IContainer container, RecommendationDocumentModel model)
    {
        container.BorderTop(1).BorderColor(ColorBorder).PaddingTop(8).Row(row =>
        {
            row.RelativeItem().Text(text =>
            {
                text.Span("Advert").FontSize(9).SemiBold();
                text.Span("ified").FontSize(9).SemiBold().FontColor(ColorGreen);
            });
            row.ConstantItem(220).AlignRight().Text($"{model.CampaignName} | Generated {model.GeneratedAtUtc:dd MMM yyyy}").FontSize(8).FontColor(ColorMuted);
        });
    }

    private static void AddDetailRow(TableDescriptor table, string label, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        table.Cell().PaddingVertical(2).Text(label).SemiBold().FontColor("#4B5563");
        table.Cell().PaddingVertical(2).Text(RecommendationPdfCopy.ToClientCopy(value));
    }

    private static string ResolveSpendColor(string channelKey)
    {
        return channelKey switch
        {
            "ooh" => ColorGreen,
            "radio" => ColorAmber,
            "digital" => ColorBlue,
            "tv" => "#6D5BD0",
            _ => ColorGreenDark
        };
    }

    private static int GetFeaturedProposalIndex(int count)
    {
        if (count <= 1)
        {
            return 0;
        }

        return count == 3 ? 1 : 0;
    }

    private static (string Text, string Background) BuildChannelBadge(string? channel)
    {
        var normalized = RecommendationPdfCopy.NormalizeRecommendationChannel(channel);
        return normalized switch
        {
            "ooh" => ("BDS", "#E6F1FB"),
            "radio" => ("RAD", ColorAmberLight),
            "digital" => ("DIG", "#EAF3FF"),
            "tv" => ("TV", "#EEE9FF"),
            _ => ("AI", ColorGreenLight)
        };
    }

    private static string[] BuildTermsSummary()
    {
        return new[]
        {
            "1. These terms become binding upon written acceptance of a proposal, issue of a purchase order or instruction, or payment.",
            "2. Payment is due within 7 days unless otherwise agreed in writing, and late payments incur interest at 2% per month calculated daily.",
            "3. All media placements remain subject to supplier availability and confirmation, and no booking is secured until payment or valid proof of payment is received.",
            "4. Advertified may substitute equivalent media placements where necessary, and supplier terms apply in addition to Advertified's terms.",
            "5. Cancellations must be submitted in writing and may incur fees of up to 50% more than 14 days before campaign start or up to 100% less than 7 days before campaign start.",
            "6. Campaign execution depends on payment, final creative approval, and supplier scheduling. Client-caused delays do not create refund rights.",
            "7. The client warrants that campaign content complies with South African law and Advertising Regulatory Board standards.",
            "8. Refunds are not standard and remain subject to supplier approval. Where applicable, refunds are usually issued as account credit.",
            "9. Advertified's total liability is limited to fees paid by the client and excludes indirect or consequential losses.",
            "10. These terms are governed by the laws of the Republic of South Africa, with jurisdiction in the Gauteng High Court."
        };
    }
}

internal sealed record PlacementSectionDocumentModel(
    string Label,
    string TotalLabel,
    IReadOnlyList<RecommendationLineDocumentModel> Placements);

public static class RecommendationPdfPreviewFactory
{
    public static byte[] GenerateSample()
    {
        var model = new RecommendationDocumentModel
        {
            ClientName = "Demo Client",
            BusinessName = "Demo Retail Group",
            CampaignName = "Autumn Launch 2026",
            PackageName = "Scale",
            SelectedBudget = 125000m,
            GeneratedAtUtc = DateTime.UtcNow,
            CampaignObjective = "Drive retail footfall and launch awareness across Gauteng.",
            SpecialRequirements = "Creative to remain family-safe and visible near commuter routes.",
            TargetAreas = new[] { "Johannesburg", "Pretoria", "Sandton" },
            TargetLanguages = new[] { "English", "isiZulu" },
            Proposals = new[]
            {
                new RecommendationProposalDocumentModel
                {
                    Label = "Proposal A",
                    Strategy = "Balanced mix",
                    Summary = "A balanced reach and frequency mix across outdoor, radio, and TV support.",
                    Rationale = "Built to balance commuter visibility with regional broadcast support while staying inside the selected budget.",
                    TotalCost = 121500m,
                    Items = new[]
                    {
                        new RecommendationLineDocumentModel
                        {
                            Channel = "Billboards and Digital Screens",
                            Title = "Sandton Drive digital screen",
                            Rationale = "Premium commuter visibility close to retail and corporate traffic.",
                            Quantity = 1,
                            Region = "Sandton, Johannesburg, Gauteng",
                            Duration = "4 weeks",
                            SlotType = "Digital screen placement",
                            Restrictions = "Subject to final site availability and artwork approval.",
                            Dimensions = "3.84m (w) x 2.24m (h)",
                            Material = "Digital creative",
                            Illuminated = "Yes",
                            TrafficCount = "1,000,000",
                            SiteNumber = "BS-113",
                            SelectionReasons = new[] { "Premium commuter route", "Retail adjacency", "Budget fit" }
                        },
                        new RecommendationLineDocumentModel
                        {
                            Channel = "Radio",
                            Title = "Kaya 959 breakfast support",
                            Rationale = "High-value urban audience support during high-attention drive and breakfast windows.",
                            Quantity = 1,
                            Region = "Gauteng",
                            Language = "English",
                            TimeBand = "Breakfast / Drive",
                            SlotType = "Radio spots",
                            Duration = "30s",
                            Flighting = "Mon-Fri for 4 weeks",
                            Restrictions = "Final station schedule subject to booking confirmation.",
                            SelectionReasons = new[] { "Strong audience fit", "Urban premium audience", "Frequency support" }
                        },
                        new RecommendationLineDocumentModel
                        {
                            Channel = "TV",
                            Title = "SABC 3 prime support",
                            Rationale = "Adds broad brand authority and premium visibility for the launch burst.",
                            Quantity = 1,
                            Region = "National",
                            TimeBand = "Prime time",
                            SlotType = "TV insertions",
                            Duration = "30s",
                            Flighting = "4 insertions / month",
                            Restrictions = "Programming and final spots subject to channel confirmation.",
                            SelectionReasons = new[] { "Broader awareness", "Premium context", "Completes media mix" }
                        }
                    }
                }
            }
        };

        return RecommendationPdfGenerator.Generate(model, null);
    }

    public static byte[] GenerateLeadSample()
    {
        var model = new RecommendationDocumentModel
        {
            ClientName = "Jozi Kitchens",
            BusinessName = "Jozi Kitchens",
            CampaignName = "Jozi Kitchens Launch Campaign",
            PackageName = "Launch",
            SelectedBudget = 100000m,
            BudgetLabel = "Package range",
            BudgetDisplayText = "R 20,000 - R 100,000",
            GeneratedAtUtc = DateTime.UtcNow,
            CampaignObjective = "Build awareness across Gauteng",
            TargetAreas = new[] { "Gauteng" },
            TargetAudienceSummary = "Ages 45-54 | Business and premium",
            TargetLanguages = new[] { "English" },
            OpportunityContext = new RecommendationOpportunityContextModel
            {
                IsLeadOutreach = true,
                ArchetypeName = "Active scaler",
                DetectedGaps = new[]
                {
                    "Your digital presence is active, but offline visibility is still too light in high-value retail zones.",
                    "You are not consistently present where premium home-improvement buyers make decisions.",
                    "Competitors with stronger physical visibility are likely capturing demand before it reaches you."
                },
                ExpectedOutcome = "A stronger mix of retail-adjacent Billboards and Digital Screens, selective radio, and digital support should increase local awareness and help convert more nearby demand.",
                WhyActNow = "Retail and home-upgrade demand is already active in Gauteng. The fastest gains come from showing up where buyers are already moving.",
                FlexibleRollout = "This can launch through our buy now, pay later structure, so visibility does not need to wait for a full upfront media payment.",
                NextStep = "Review the three proposal paths and choose the one that fits your current growth pace best."
            },
            Proposals = new[]
            {
                new RecommendationProposalDocumentModel
                {
                    Label = "Proposal A",
                    Strategy = "Balanced mix",
                    AcceptUrl = "https://www.advertified.com/proposal/demo/proposal-a",
                    Summary = "A balanced launch mix across Billboards and Digital Screens, radio, and digital support.",
                    Rationale = "This plan gives Jozi Kitchens consistent visibility at premium shopping destinations across Gauteng, supported by radio presence and digital continuity.",
                    TotalCost = 45280m,
                    Items = new[]
                    {
                        new RecommendationLineDocumentModel { Channel = "OOH", Title = "Benmore Centre - Digital Screen", TotalCost = 10200m, Quantity = 1, Region = "Sandton, Gauteng", SelectionReasons = new[] { "Strong geo match", "Audience fit" } },
                        new RecommendationLineDocumentModel { Channel = "OOH", Title = "Bryanston Shopping Centre - Digital Screen", TotalCost = 10200m, Quantity = 1, Region = "Bryanston, Gauteng", SelectionReasons = new[] { "Strong geo match", "Audience fit" } },
                        new RecommendationLineDocumentModel { Channel = "OOH", Title = "Centurion Lifestyle Centre - Digital Screen", TotalCost = 10180m, Quantity = 1, Region = "Centurion, Gauteng", SelectionReasons = new[] { "Strong geo match", "Audience fit" } },
                        new RecommendationLineDocumentModel { Channel = "Radio", Title = "Kaya 959 - Retail Package", TotalCost = 14700m, Quantity = 1, Region = "Johannesburg", SelectionReasons = new[] { "Urban premium audience", "Audience match" } },
                        new RecommendationLineDocumentModel { Channel = "Digital", Title = "Digital amplification support", TotalCost = 0m, Quantity = 1, Region = "Gauteng", SelectionReasons = new[] { "Included support" } }
                    }
                },
                new RecommendationProposalDocumentModel
                {
                    Label = "Proposal B",
                    Strategy = "Billboards and Digital Screens-led reach",
                    AcceptUrl = "https://www.advertified.com/proposal/demo/proposal-b",
                    Summary = "Maximum visual presence across Gauteng's premium retail destinations.",
                    Rationale = "This is the strongest visual-presence option, giving Jozi Kitchens repeated exposure in high-value retail and commuter environments while retaining enough broadcast support to extend awareness.",
                    TotalCost = 71320m,
                    Items = new[]
                    {
                        new RecommendationLineDocumentModel { Channel = "OOH", Title = "89 Grayston Drive - Digital Screen", TotalCost = 14200m, Quantity = 1, Region = "Sandton, Gauteng", SelectionReasons = new[] { "Strong geo match", "Audience fit" } },
                        new RecommendationLineDocumentModel { Channel = "OOH", Title = "Benmore Centre - Digital Screen", TotalCost = 14200m, Quantity = 1, Region = "Sandton, Gauteng", SelectionReasons = new[] { "Strong geo match", "Audience fit" } },
                        new RecommendationLineDocumentModel { Channel = "OOH", Title = "Bryanston Shopping Centre - Digital Screen", TotalCost = 14200m, Quantity = 1, Region = "Bryanston, Gauteng", SelectionReasons = new[] { "Strong geo match", "Audience fit" } },
                        new RecommendationLineDocumentModel { Channel = "OOH", Title = "Morningside Centre - Digital Screen", TotalCost = 14200m, Quantity = 1, Region = "Sandton, Gauteng", SelectionReasons = new[] { "Strong geo match", "Audience fit" } },
                        new RecommendationLineDocumentModel { Channel = "Radio", Title = "Kaya 959 - Retail Package", TotalCost = 14520m, Quantity = 1, Region = "Johannesburg", SelectionReasons = new[] { "Urban premium audience", "Audience match" } }
                    }
                },
                new RecommendationProposalDocumentModel
                {
                    Label = "Proposal C",
                    Strategy = "Radio-led frequency",
                    AcceptUrl = "https://www.advertified.com/proposal/demo/proposal-c",
                    Summary = "The widest reach plan, built for repeated audio frequency with supporting screen presence.",
                    Rationale = "This plan gives Jozi Kitchens broad audio reach through multiple radio placements, while still maintaining premium in-market visibility with selected digital screens.",
                    TotalCost = 100000m,
                    Items = new[]
                    {
                        new RecommendationLineDocumentModel { Channel = "OOH", Title = "Benmore Centre - Digital Screen", TotalCost = 10000m, Quantity = 1, Region = "Sandton, Gauteng", SelectionReasons = new[] { "Strong geo match" } },
                        new RecommendationLineDocumentModel { Channel = "OOH", Title = "Hyde Park Corner - Digital Screen", TotalCost = 10000m, Quantity = 1, Region = "Sandton, Gauteng", SelectionReasons = new[] { "Strong geo match" } },
                        new RecommendationLineDocumentModel { Channel = "OOH", Title = "Morningside Centre - Digital Screen", TotalCost = 10000m, Quantity = 1, Region = "Sandton, Gauteng", SelectionReasons = new[] { "Strong geo match" } },
                        new RecommendationLineDocumentModel { Channel = "Radio", Title = "Kaya 959 - Retail Package", TotalCost = 17500m, Quantity = 1, Region = "Johannesburg", SelectionReasons = new[] { "Urban premium audience" } },
                        new RecommendationLineDocumentModel { Channel = "Radio", Title = "Kaya 959 - Spot Package", TotalCost = 17500m, Quantity = 1, Region = "Johannesburg", SelectionReasons = new[] { "High frequency" } },
                        new RecommendationLineDocumentModel { Channel = "Radio", Title = "Metro FM - Spot Package", TotalCost = 17500m, Quantity = 1, Region = "National", SelectionReasons = new[] { "Mass reach", "Urban market" } },
                        new RecommendationLineDocumentModel { Channel = "Radio", Title = "SAfm - Spot Package", TotalCost = 17500m, Quantity = 1, Region = "National", SelectionReasons = new[] { "Professional audience", "Opinion leaders" } }
                    }
                }
            }
        };

        return LeadOutreachPdfGenerator.Generate(model, null);
    }
}

internal sealed class RecommendationDocumentModel
{
    public string ClientName { get; init; } = string.Empty;
    public string? BusinessName { get; init; }
    public string CampaignName { get; init; } = string.Empty;
    public string? CampaignApprovalsUrl { get; init; }
    public string PackageName { get; init; } = string.Empty;
    public decimal SelectedBudget { get; init; }
    public string BudgetLabel { get; init; } = "Budget";
    public string BudgetDisplayText { get; init; } = string.Empty;
    public DateTime GeneratedAtUtc { get; init; }
    public string? CampaignObjective { get; init; }
    public string? SpecialRequirements { get; init; }
    public IReadOnlyList<string> TargetAreas { get; init; } = Array.Empty<string>();
    public string? TargetAudienceSummary { get; init; }
    public IReadOnlyList<string> TargetLanguages { get; init; } = Array.Empty<string>();
    public RecommendationOpportunityContextModel? OpportunityContext { get; init; }
    public IReadOnlyList<RecommendationProposalDocumentModel> Proposals { get; init; } = Array.Empty<RecommendationProposalDocumentModel>();
}

internal sealed class RecommendationOpportunityContextModel
{
    public bool IsLeadOutreach { get; init; }
    public string? ArchetypeName { get; init; }
    public string? IndustryProfileName { get; init; }
    public string? IndustryMessagingAngle { get; init; }
    public IReadOnlyList<string> IndustryGuardrails { get; init; } = Array.Empty<string>();
    public string? IndustryRecommendedCta { get; init; }
    public string? WhoWeAre { get; init; }
    public IReadOnlyList<string> ResearchBasis { get; init; } = Array.Empty<string>();
    public string? LastResearchedAtUtc { get; init; }
    public string? SocialQualityNote { get; init; }
    public IReadOnlyList<string> DetectedGaps { get; init; } = Array.Empty<string>();
    public string? LeadInsightSummary { get; init; }
    public string? ExpectedOutcome { get; init; }
    public string? WhyActNow { get; init; }
    public string? FlexibleRollout { get; init; }
    public string? NextStep { get; init; }
}

internal sealed class RecommendationProposalDocumentModel
{
    public string Label { get; init; } = "Proposal";
    public string? Strategy { get; init; }
    public string? AcceptUrl { get; init; }
    public string Summary { get; init; } = string.Empty;
    public string Rationale { get; init; } = string.Empty;
    public decimal TotalCost { get; init; }
    public IReadOnlyList<RecommendationLineDocumentModel> Items { get; init; } = Array.Empty<RecommendationLineDocumentModel>();
}

internal sealed class RecommendationLineDocumentModel
{
    public string Channel { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Rationale { get; init; } = string.Empty;
    public decimal TotalCost { get; init; }
    public int Quantity { get; init; }
    public string? Region { get; init; }
    public string? Language { get; init; }
    public string? ShowDaypart { get; init; }
    public string? TimeBand { get; init; }
    public string? SlotType { get; init; }
    public string? Duration { get; init; }
    public string? AppliedDuration { get; init; }
    public string? Flighting { get; init; }
    public string? RequestedStartDate { get; init; }
    public string? RequestedEndDate { get; init; }
    public string? ResolvedStartDate { get; init; }
    public string? ResolvedEndDate { get; init; }
    public string? CommercialExplanation { get; init; }
    public string? Restrictions { get; init; }
    public string? Dimensions { get; init; }
    public string? Material { get; init; }
    public string? Illuminated { get; init; }
    public string? TrafficCount { get; init; }
    public string? TargetAudience { get; init; }
    public string? AudienceAgeSkew { get; init; }
    public string? AudienceGenderSkew { get; init; }
    public string? AudienceLsmRange { get; init; }
    public string? ListenershipDaily { get; init; }
    public string? ListenershipWeekly { get; init; }
    public string? ListenershipPeriod { get; init; }
    public string? SiteNumber { get; init; }
    public string? ItemNotes { get; init; }
    public IReadOnlyList<string> SelectionReasons { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> PolicyFlags { get; init; } = Array.Empty<string>();
}
