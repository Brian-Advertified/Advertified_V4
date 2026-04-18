using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
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

                col.Item().PaddingTop(3).Text("advertise now - pay later").FontSize(8).LetterSpacing(1.2f).FontColor(ColorMuted);
            });

            row.ConstantItem(220).AlignRight().Column(col =>
            {
                col.Item().AlignRight().Background(ColorGreenLight).PaddingVertical(4).PaddingHorizontal(10).Text("Client Copy").FontSize(9).SemiBold().FontColor(ColorGreen).LetterSpacing(1.2f);
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
                .FontSize(9).SemiBold().LetterSpacing(1.1f).FontColor(ColorMuted);
            column.Item().Text(model.CampaignName).FontSize(28).SemiBold();
            column.Item().Text(
                    !string.IsNullOrWhiteSpace(model.CampaignObjective)
                        ? $"Prepared for {ResolveBusinessReference(model)} - {ToClientCopy(model.CampaignObjective)}."
                        : $"Prepared for {ResolveBusinessReference(model)}.")
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
            column.Item().Text($"We found where {ResolveBusinessReference(model)} is losing customers, and built the campaign to fix it.").FontColor(ColorMuted);

            var topGaps = model.OpportunityContext!.DetectedGaps
                .Where(static gap => !string.IsNullOrWhiteSpace(gap))
                .Take(3)
                .Select(gap => TruncateClientCopy(gap, 150))
                .ToArray();
            foreach (var gap in topGaps)
            {
                column.Item().Text($"- {gap}").FontColor(ColorMuted);
            }

            if (!string.IsNullOrWhiteSpace(model.OpportunityContext.ExpectedOutcome))
            {
                column.Item().Text(TruncateClientCopy(model.OpportunityContext.ExpectedOutcome, 220)).FontColor(ColorMuted);
            }
        });
    }

    private static void ComposeCampaignOverview(IContainer container, RecommendationDocumentModel model)
    {
        var overviewItems = new List<(string Label, string Value)>
        {
            ("Client", ResolveBusinessReference(model)),
            ("Objective", ToClientCopy(model.CampaignObjective) is { Length: > 0 } objective ? objective : "Campaign growth"),
            ("Region", model.TargetAreas.Count > 0 ? string.Join(", ", model.TargetAreas.Select(ToClientCopy)) : "South Africa"),
            ("Target audience", !string.IsNullOrWhiteSpace(model.TargetAudienceSummary) ? ToClientCopy(model.TargetAudienceSummary) : "Audience not specified"),
            ("Language", model.TargetLanguages.Count > 0 ? string.Join(", ", model.TargetLanguages.Select(ToClientCopy)) : "Language not specified"),
            ("Payment", "Buy now, pay later available")
        };

        container.Column(column =>
        {
            column.Spacing(10);
            column.Item().Text("Campaign overview").FontSize(9).SemiBold().LetterSpacing(1.1f).FontColor(ColorMuted);
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
                        cell.Item().Text(item.Label).FontSize(8).SemiBold().LetterSpacing(1.0f).FontColor(ColorMuted);
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
            column.Item().Text("Choose your growth path").FontSize(9).SemiBold().LetterSpacing(1.1f).FontColor(ColorMuted);
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
        var mediaCounts = BuildProposalMediaCounts(proposal);

        container.Border(1.5f).BorderColor(featured ? ColorGreen : ColorBorder).Background(ColorWhite).Column(column =>
        {
            if (featured)
            {
                column.Item().AlignRight().Background(ColorAmber).PaddingVertical(3).PaddingHorizontal(8).Text("Recommended").FontSize(7).FontColor(ColorWhite).SemiBold().LetterSpacing(1.0f);
            }

            column.Item().Background(featured ? ColorGreen : ColorSurface).Padding(14).Column(header =>
            {
                header.Spacing(3);
                header.Item().Text(proposal.Label).FontSize(8).SemiBold().LetterSpacing(1.0f).FontColor(featured ? ColorWhite : ColorMuted);
                header.Item().Text(ToClientCopy(proposal.Strategy ?? "Recommendation option")).FontSize(11).SemiBold().FontColor(featured ? ColorWhite : ColorInk);
            });

            column.Item().Padding(14).Column(body =>
            {
                body.Spacing(8);
                body.Item().Text("Campaign total").FontSize(8).SemiBold().LetterSpacing(1.0f).FontColor(ColorMuted);
                body.Item().Text(FormatCurrency(proposal.TotalCost)).FontSize(18).SemiBold();

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
                    col.Item().Text(proposal.Label).FontSize(10).SemiBold().LetterSpacing(1.0f).FontColor(ColorMuted);
                    col.Item().Text(ToClientCopy(proposal.Strategy ?? "Recommendation option")).FontSize(18).SemiBold();
                    col.Item().Text(BuildProposalSubheading(model, proposal)).FontSize(10).FontColor(ColorMuted);
                });
                row.ConstantItem(130).AlignRight().Text(FormatCurrency(proposal.TotalCost)).FontSize(18).SemiBold().FontColor(ColorGreen);
            });

            section.Item().Background(ColorGreenLight).Padding(12).BorderLeft(3).BorderColor(ColorGreen).Text(ToClientCopy(!string.IsNullOrWhiteSpace(proposal.Rationale) ? proposal.Rationale : proposal.Summary))
                .FontSize(10)
                .FontColor(ColorGreenDark);

            section.Item().Element(item => ComposeBudgetSplit(item, proposal));

            section.Item().Text("Your placements").FontSize(9).SemiBold().LetterSpacing(1.1f).FontColor(ColorMuted);
            foreach (var item in proposal.Items)
            {
                section.Item().Element(card => ComposePlacementCard(card, model, item));
            }

            if (!string.IsNullOrWhiteSpace(proposal.AcceptUrl))
            {
                section.Item().Hyperlink(proposal.AcceptUrl).Background(ColorInk).Padding(16).Row(row =>
                {
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text($"Accept {proposal.Label}").FontSize(12).SemiBold().FontColor(ColorWhite);
                        col.Item().Text($"{FormatCurrency(proposal.TotalCost)} | Buy now, pay later available").FontSize(9).FontColor("#D1D5DB");
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
        var split = BuildChannelSpendSplit(proposal);
        if (split.Count == 0)
        {
            return;
        }

        container.Background(ColorSurface).Padding(14).Column(column =>
        {
            column.Spacing(8);
            column.Item().Text("Budget split").FontSize(8).SemiBold().LetterSpacing(1.0f).FontColor(ColorMuted);
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

                        table.Cell().Height(8).Background(entry.Color);
                        table.Cell().Height(8).Background(ColorBorder);
                    });
                    row.ConstantItem(34).AlignRight().Text($"{entry.Percent}%").FontSize(9).SemiBold();
                });
            }
        });
    }

    private static void ComposePlacementCard(IContainer container, RecommendationDocumentModel model, RecommendationLineDocumentModel item)
    {
        var details = BuildPlacementTags(item);
        var clientSummary = BuildClientSelectionSummary(model, item);
        var badge = BuildChannelBadge(item.Channel);

        container.Border(1).BorderColor(ColorBorder).Background(ColorSurface).Padding(12).Row(row =>
        {
            row.Spacing(10);
            row.ConstantItem(34).Background(badge.Background).AlignMiddle().AlignCenter().PaddingVertical(8).Text(badge.Text).FontSize(8).SemiBold();
            row.RelativeItem().Column(col =>
            {
                col.Spacing(4);
                col.Item().Text(ToClientCopy(item.Title)).FontSize(11).SemiBold();
                col.Item().Text(BuildPlacementLocation(item)).FontSize(9).FontColor(ColorMuted);
                if (details.Count > 0)
                {
                    col.Item().Row(tagRow =>
                    {
                        tagRow.Spacing(4);
                        foreach (var tag in details.Take(3))
                        {
                            tagRow.AutoItem().Border(1).BorderColor(ColorBorder).Background(ColorWhite).PaddingVertical(2).PaddingHorizontal(6).Text(tag).FontSize(7).FontColor(ColorMuted);
                        }
                    });
                }

                foreach (var line in clientSummary.Take(2))
                {
                    col.Item().Text(line).FontSize(8).FontColor(ColorMuted);
                }
            });
            row.ConstantItem(70).AlignRight().Column(col =>
            {
                col.Item().Text($"Qty: {Math.Max(1, item.Quantity)}").FontSize(8).SemiBold().FontColor(ColorMuted);
                if (item.TotalCost > 0)
                {
                    col.Item().PaddingTop(4).Text(FormatCurrency(item.TotalCost)).FontSize(9).SemiBold();
                }
                else
                {
                    col.Item().PaddingTop(4).Text("Included").FontSize(9).SemiBold();
                }
            });
        });
    }

    private static void ComposeTermsSummary(IContainer container)
    {
        container.Column(terms =>
        {
            terms.Spacing(10);
            terms.Item().Text("Terms and conditions summary").FontSize(9).SemiBold().LetterSpacing(1.1f).FontColor(ColorMuted);
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
        table.Cell().PaddingVertical(2).Text(ToClientCopy(value));
    }

    private static IReadOnlyList<(string Channel, int Quantity)> BuildProposalMediaCounts(RecommendationProposalDocumentModel proposal)
    {
        return proposal.Items
            .Where(item => !string.IsNullOrWhiteSpace(item.Channel) && !string.Equals(item.Channel, "Studio", StringComparison.OrdinalIgnoreCase))
            .GroupBy(item => FormatChannelLabel(item.Channel))
            .Select(group =>
            {
                var quantity = group.Sum(item => item.Quantity > 0 ? item.Quantity : 1);
                return (Channel: group.Key, Quantity: quantity);
            })
            .OrderBy(item => item.Channel, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyList<(string Label, decimal Percent, string Color)> BuildChannelSpendSplit(RecommendationProposalDocumentModel proposal)
    {
        var channelSpend = proposal.Items
            .Where(item => item.TotalCost > 0 && !string.Equals(item.Channel, "Studio", StringComparison.OrdinalIgnoreCase))
            .GroupBy(item => NormalizeSpendChannel(item.Channel))
            .Select(group => new
            {
                Key = group.Key,
                Total = group.Sum(item => item.TotalCost)
            })
            .Where(entry => entry.Total > 0)
            .ToArray();

        var total = channelSpend.Sum(entry => entry.Total);
        if (total <= 0)
        {
            return Array.Empty<(string Label, decimal Percent, string Color)>();
        }

        return channelSpend
            .Select(entry =>
            {
                var label = entry.Key switch
                {
                    "ooh" => "Billboards and Digital",
                    "radio" => "Radio",
                    "digital" => "Digital (online)",
                    "tv" => "TV",
                    _ => ToClientCopy(entry.Key)
                };
                var color = entry.Key switch
                {
                    "ooh" => ColorGreen,
                    "radio" => ColorAmber,
                    "digital" => ColorBlue,
                    "tv" => "#6D5BD0",
                    _ => ColorGreenDark
                };
                var percent = Math.Round((entry.Total / total) * 100m, 0, MidpointRounding.AwayFromZero);
                return (Label: label, Percent: percent, Color: color);
            })
            .OrderByDescending(entry => entry.Percent)
            .ToArray();
    }

    private static string NormalizeSpendChannel(string? channel)
    {
        var normalized = NormalizeRecommendationChannel(channel);
        return normalized switch
        {
            "ooh" => "ooh",
            "radio" => "radio",
            "digital" => "digital",
            "tv" => "tv",
            _ => normalized
        };
    }

    private static string BuildProposalSubheading(RecommendationDocumentModel model, RecommendationProposalDocumentModel proposal)
    {
        var placements = proposal.Items.Count(item => !string.Equals(item.Channel, "Studio", StringComparison.OrdinalIgnoreCase));
        var channels = proposal.Items
            .Where(item => !string.Equals(item.Channel, "Studio", StringComparison.OrdinalIgnoreCase))
            .Select(item => FormatChannelLabel(item.Channel))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var areas = proposal.Items
            .Select(item => item.Region)
            .Where(area => !string.IsNullOrWhiteSpace(area))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(2)
            .Select(area => ToClientCopy(area!))
            .ToArray();

        var areaText = areas.Length > 0
            ? string.Join(" and ", areas)
            : (model.TargetAreas.Count > 0 ? string.Join(", ", model.TargetAreas.Select(ToClientCopy)) : "South Africa");

        return $"{placements} placements across {string.Join(", ", channels)} | {areaText}";
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
        var normalized = NormalizeRecommendationChannel(channel);
        return normalized switch
        {
            "ooh" => ("OOH", "#E6F1FB"),
            "radio" => ("RAD", ColorAmberLight),
            "digital" => ("DIG", "#EAF3FF"),
            "tv" => ("TV", "#EEE9FF"),
            _ => ("AI", ColorGreenLight)
        };
    }

    private static string BuildPlacementLocation(RecommendationLineDocumentModel item)
    {
        var parts = new[]
        {
            item.Region,
            item.TimeBand,
            item.Duration
        }
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(value => ToClientCopy(value!))
            .ToArray();

        return parts.Length > 0 ? string.Join(" | ", parts) : ToClientCopy(item.Rationale);
    }

    private static IReadOnlyList<string> BuildPlacementTags(RecommendationLineDocumentModel item)
    {
        var tags = new List<string>();
        tags.AddRange(item.SelectionReasons.Select(ToClientCopy));

        if (!string.IsNullOrWhiteSpace(item.Language))
        {
            tags.Add(ToClientCopy(item.Language));
        }

        if (!string.IsNullOrWhiteSpace(item.ShowDaypart))
        {
            tags.Add(ToClientCopy(item.ShowDaypart));
        }

        return tags
            .Where(static tag => !string.IsNullOrWhiteSpace(tag))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(4)
            .ToArray();
    }

    private static string NormalizeRecommendationChannel(string? channel)
    {
        var normalized = (channel ?? string.Empty).Trim().ToLowerInvariant();
        if (normalized.Contains("ooh", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("billboard", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("out of home", StringComparison.OrdinalIgnoreCase))
        {
            return "ooh";
        }

        if (normalized.Contains("radio", StringComparison.OrdinalIgnoreCase))
        {
            return "radio";
        }

        if (normalized.Contains("tv", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("television", StringComparison.OrdinalIgnoreCase))
        {
            return "tv";
        }

        if (normalized.Contains("digital", StringComparison.OrdinalIgnoreCase))
        {
            return "digital";
        }

        return normalized;
    }

    private static string FormatCurrency(decimal amount)
    {
        return $"R {amount.ToString("N2", CultureInfo.GetCultureInfo("en-ZA"))}";
    }

    private static string ResolveBusinessReference(RecommendationDocumentModel model)
    {
        return !string.IsNullOrWhiteSpace(model.BusinessName)
            ? model.BusinessName.Trim()
            : (!string.IsNullOrWhiteSpace(model.ClientName) ? model.ClientName.Trim() : "this business");
    }

    private static string FormatChannelLabel(string? channel)
    {
        if (string.IsNullOrWhiteSpace(channel))
        {
            return string.Empty;
        }

        return string.Equals(channel.Trim(), "OOH", StringComparison.OrdinalIgnoreCase)
            ? "Billboards and Digital Screens"
            : ToClientCopy(channel.Trim());
    }

    private static string ToClientCopy(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value
            .Replace("â€™", "'")
            .Replace("â€˜", "'")
            .Replace("â€œ", "\"")
            .Replace("â€", "\"")
            .Replace("â€“", "-")
            .Replace("â€”", "-")
            .Replace("\u00A0", " ");
        normalized = Regex.Replace(normalized, "\\s+", " ").Trim();

        return Regex.Replace(normalized, "\\booh\\b", "Billboards and Digital Screens", RegexOptions.IgnoreCase);
    }

    private static string TruncateClientCopy(string? value, int maxLength)
    {
        var clean = ToClientCopy(value);
        if (clean.Length <= maxLength)
        {
            return clean;
        }

        return clean[..Math.Max(0, maxLength - 3)].TrimEnd() + "...";
    }

    private static IReadOnlyList<string> BuildClientSelectionSummary(RecommendationDocumentModel model, RecommendationLineDocumentModel item)
    {
        var lines = new List<string>();

        var audience = BuildAudienceFocus(model, item);
        if (!string.IsNullOrWhiteSpace(audience))
        {
            lines.Add($"Who we are targeting: {ToClientCopy(audience)}");
        }

        var scale = BuildAudienceScale(item);
        if (!string.IsNullOrWhiteSpace(scale))
        {
            lines.Add($"Estimated audience size: {ToClientCopy(scale)}");
        }

        var fit = BuildFitNarrative(model, item);
        if (!string.IsNullOrWhiteSpace(fit))
        {
            lines.Add($"Why this fits: {ToClientCopy(fit)}");
        }

        return lines;
    }

    private static string? BuildAudienceFocus(RecommendationDocumentModel model, RecommendationLineDocumentModel item)
    {
        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(item.TargetAudience))
        {
            parts.Add(item.TargetAudience.Trim());
        }
        else if (!string.IsNullOrWhiteSpace(model.TargetAudienceSummary))
        {
            parts.Add(model.TargetAudienceSummary.Trim());
        }

        var qualifiers = new List<string>();
        if (!string.IsNullOrWhiteSpace(item.Region))
        {
            qualifiers.Add(item.Region.Trim());
        }

        if (!string.IsNullOrWhiteSpace(item.Language))
        {
            qualifiers.Add($"{item.Language.Trim()} speakers");
        }

        if (!string.IsNullOrWhiteSpace(item.AudienceAgeSkew))
        {
            qualifiers.Add(item.AudienceAgeSkew.Trim());
        }

        if (!string.IsNullOrWhiteSpace(item.AudienceLsmRange))
        {
            qualifiers.Add($"LSM {item.AudienceLsmRange.Trim()}");
        }

        if (!string.IsNullOrWhiteSpace(item.AudienceGenderSkew))
        {
            qualifiers.Add(item.AudienceGenderSkew.Trim());
        }

        if (parts.Count == 0 && qualifiers.Count == 0)
        {
            return null;
        }

        if (parts.Count == 0)
        {
            return string.Join(" | ", qualifiers);
        }

        if (qualifiers.Count == 0)
        {
            return string.Join(" | ", parts);
        }

        return $"{string.Join(" | ", parts)} | {string.Join(" | ", qualifiers)}";
    }

    private static string? BuildAudienceScale(RecommendationLineDocumentModel item)
    {
        if (TryFormatWholeNumber(item.ListenershipWeekly, out var weekly))
        {
            var period = !string.IsNullOrWhiteSpace(item.ListenershipPeriod)
                ? $" {item.ListenershipPeriod.Trim().ToLowerInvariant()}"
                : " weekly";
            return $"Approximately {weekly} listeners{period}.";
        }

        if (TryFormatWholeNumber(item.ListenershipDaily, out var daily))
        {
            return $"Approximately {daily} listeners per day.";
        }

        if (TryFormatWholeNumber(item.TrafficCount, out var traffic))
        {
            return $"Approximately {traffic} people pass this site.";
        }

        return null;
    }

    private static string? BuildFitNarrative(RecommendationDocumentModel model, RecommendationLineDocumentModel item)
    {
        var fitParts = new List<string>();

        if (!string.IsNullOrWhiteSpace(item.Region) && model.TargetAreas.Count > 0)
        {
            fitParts.Add("It gives us visibility in one of the campaign's target areas");
        }

        if (!string.IsNullOrWhiteSpace(item.Language) && model.TargetLanguages.Count > 0)
        {
            fitParts.Add("It supports the language mix requested for the campaign");
        }

        if (!string.IsNullOrWhiteSpace(item.TimeBand))
        {
            fitParts.Add($"It places the campaign in the {item.TimeBand.Trim()} window");
        }

        if (!string.IsNullOrWhiteSpace(item.SelectionReasons.FirstOrDefault()))
        {
            fitParts.Add(RewriteSelectionReason(item.SelectionReasons.First()));
        }
        else if (!string.IsNullOrWhiteSpace(item.Rationale))
        {
            fitParts.Add(item.Rationale.Trim());
        }

        var cleaned = fitParts
            .Where(static part => !string.IsNullOrWhiteSpace(part))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return cleaned.Length == 0 ? null : string.Join(". ", cleaned) + ".";
    }

    private static string RewriteSelectionReason(string reason)
    {
        return reason.Trim() switch
        {
            "Strong geography match" => "The location matches the market we want to reach",
            "Good regional alignment" => "The placement lines up well with the target region",
            "Audience profile overlap" => "The audience profile matches the people this campaign is trying to reach",
            "Language or audience fit" => "The audience and language fit the brief",
            "Matches requested channel mix" => "It strengthens the channel mix chosen for this campaign",
            "Supports requested mix target" => "It helps keep the campaign balanced across the selected channels",
            "Fits comfortably within budget" => "It stays comfortably within the approved budget",
            "Fixed supplier package investment" => "It comes as a bundled media package that keeps planning simple",
            "Per-spot rate card pricing" => "It uses standard spot pricing for flexible scheduling",
            "High-impact radio daypart" => "It runs in a high-attention radio slot",
            "Supports higher-band radio policy" => "It supports broader radio reach at this budget level",
            "Billboards and Digital Screens prioritized for visibility" => "It adds strong visual presence in-market",
            "Adds visible market presence" => "It helps the brand stay visible in the target area",
            _ => reason.Trim()
        };
    }

    private static bool TryFormatWholeNumber(string? value, out string formatted)
    {
        formatted = string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var digits = Regex.Replace(value, "[^0-9]", string.Empty);
        if (!long.TryParse(digits, out var number) || number <= 0)
        {
            return false;
        }

        formatted = number.ToString("N0", CultureInfo.GetCultureInfo("en-ZA"));
        return true;
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
    public string? Flighting { get; init; }
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
