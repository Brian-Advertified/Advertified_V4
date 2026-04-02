using System.Globalization;
using System.Text.RegularExpressions;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Advertified.App.Campaigns;

internal static class RecommendationPdfGenerator
{
    internal static byte[] Generate(RecommendationDocumentModel model, string? logoPath)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        return Document.Create(document =>
        {
            document.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(32);
                page.DefaultTextStyle(text => text.FontSize(10).FontColor("#111111").FontFamily("Arial"));

                page.Header().PaddingBottom(16).Row(row =>
                {
                    row.RelativeItem().Column(col =>
                    {
                        if (!string.IsNullOrWhiteSpace(logoPath) && File.Exists(logoPath))
                        {
                            col.Item().MaxHeight(22).Image(logoPath);
                        }
                        else
                        {
                            col.Item().Text("Advertified").SemiBold().FontSize(18);
                        }

                        col.Item().PaddingTop(6).Text("Recommendation Pack").SemiBold().FontSize(20);
                        col.Item().Text(model.CampaignName).FontColor("#4B5563");
                    });

                    row.ConstantItem(220).AlignRight().Column(col =>
                    {
                        col.Item().Text("CLIENT COPY").SemiBold().FontSize(22);
                        col.Item().Text($"Generated: {model.GeneratedAtUtc:dd MMM yyyy HH:mm} UTC").FontColor("#4B5563");
                        col.Item().Text($"Package: {model.PackageName}").FontColor("#4B5563");
                        col.Item().Text($"Budget: {FormatCurrency(model.SelectedBudget)}").SemiBold();
                    });
                });

                page.Content().Column(column =>
                {
                    column.Spacing(12);

                    column.Item().Border(1).BorderColor("#D1D5DB").Padding(12).Column(summary =>
                    {
                        summary.Item().Text("Campaign Summary").SemiBold().FontSize(12);
                        summary.Item().PaddingTop(6).Text($"Client: {model.ClientName}");
                        if (!string.IsNullOrWhiteSpace(model.BusinessName))
                        {
                            summary.Item().Text($"Business: {model.BusinessName}");
                        }

                        if (!string.IsNullOrWhiteSpace(model.CampaignObjective))
                        {
                            summary.Item().Text($"Objective: {model.CampaignObjective}").FontColor("#4B5563");
                        }

                        if (model.TargetAreas.Count > 0)
                        {
                            summary.Item().Text($"Target areas: {string.Join(", ", model.TargetAreas)}").FontColor("#4B5563");
                        }

                        if (model.TargetLanguages.Count > 0)
                        {
                            summary.Item().Text($"Target languages: {string.Join(", ", model.TargetLanguages)}").FontColor("#4B5563");
                        }

                        if (!string.IsNullOrWhiteSpace(model.SpecialRequirements))
                        {
                            summary.Item().Text($"Campaign notes: {model.SpecialRequirements}").FontColor("#4B5563");
                        }
                    });

                    if (model.Proposals.Count > 0)
                    {
                        column.Item().Border(1).BorderColor("#D1D5DB").Padding(12).Column(proposalSummary =>
                        {
                            proposalSummary.Spacing(6);
                            proposalSummary.Item().Text("Recommended options").SemiBold().FontSize(12);

                            foreach (var proposal in model.Proposals)
                            {
                                proposalSummary.Item().Row(row =>
                                {
                                    row.RelativeItem().Text($"{proposal.Label} | {ToClientCopy(proposal.Strategy ?? "Recommendation option")}");
                                    row.ConstantItem(150).AlignRight().Text(FormatCurrency(proposal.TotalCost)).SemiBold();
                                });

                                if (!string.IsNullOrWhiteSpace(proposal.AcceptUrl))
                                {
                                    proposalSummary.Item()
                                        .PaddingBottom(2)
                                        .Hyperlink(proposal.AcceptUrl)
                                        .Text($"Accept {proposal.Label}");
                                }
                            }
                        });

                        // Always keep page 1 as an overview page before detailed recommendation pages.
                        column.Item().PageBreak();
                    }

                    for (var index = 0; index < model.Proposals.Count; index++)
                    {
                        var proposal = model.Proposals[index];
                        if (index > 0)
                        {
                            column.Item().PageBreak();
                        }

                        column.Item().Border(1).BorderColor("#D1D5DB").Padding(12).Column(section =>
                        {
                            section.Spacing(10);

                            section.Item().Row(row =>
                            {
                                row.RelativeItem().Column(col =>
                                {
                                    col.Item().Text(proposal.Label).SemiBold().FontSize(14);
                                    if (!string.IsNullOrWhiteSpace(proposal.Strategy))
                                    {
                                        col.Item().Text(ToClientCopy(proposal.Strategy)).FontColor("#4B5563");
                                    }
                                });

                                row.ConstantItem(160).AlignRight().Column(col =>
                                {
                                    col.Item().Text("Campaign total").FontColor("#4B5563");
                                    col.Item().Text(FormatCurrency(proposal.TotalCost)).SemiBold().FontSize(14);
                                });
                            });

                            section.Item().Text(ToClientCopy(proposal.Summary)).SemiBold();
                            if (!string.IsNullOrWhiteSpace(proposal.Rationale))
                            {
                                section.Item().Text(ToClientCopy(proposal.Rationale)).FontColor("#4B5563");
                            }

                            if (!string.IsNullOrWhiteSpace(proposal.AcceptUrl))
                            {
                                section.Item()
                                    .Border(1)
                                    .BorderColor("#123A33")
                                    .Background("#E8F5EF")
                                    .Padding(8)
                                    .Hyperlink(proposal.AcceptUrl)
                                    .Text($"Accept {proposal.Label}");
                            }

                            foreach (var item in proposal.Items)
                            {
                                section.Item().Border(1).BorderColor("#E5E7EB").Background("#F8FAFC").Padding(10).Column(itemCol =>
                                {
                                    itemCol.Spacing(6);

                                    itemCol.Item().Row(row =>
                                    {
                                        row.RelativeItem().Column(col =>
                                        {
                                            col.Item().Text($"{FormatChannelLabel(item.Channel)} | {ToClientCopy(item.Title)}").SemiBold();
                                            if (!string.IsNullOrWhiteSpace(item.Rationale))
                                            {
                                                col.Item().Text(ToClientCopy(item.Rationale)).FontColor("#4B5563");
                                            }
                                        });

                                        row.ConstantItem(130).AlignRight().Column(col =>
                                        {
                                            col.Item().Text($"Qty: {item.Quantity}").FontColor("#4B5563");
                                        });
                                    });

                                    itemCol.Item().Table(table =>
                                    {
                                        table.ColumnsDefinition(columns =>
                                        {
                                            columns.ConstantColumn(120);
                                            columns.RelativeColumn();
                                        });

                                        AddDetailRow(table, "Region", item.Region);
                                        AddDetailRow(table, "Language", item.Language);
                                        AddDetailRow(table, "Time band", item.TimeBand);
                                        AddDetailRow(table, "Slot type", item.SlotType);
                                        AddDetailRow(table, "Duration", item.Duration);
                                        AddDetailRow(table, "Flighting", item.Flighting);
                                        AddDetailRow(table, "Restrictions", item.Restrictions);
                                        AddDetailRow(table, "Dimensions", item.Dimensions);
                                        AddDetailRow(table, "Material", item.Material);
                                        AddDetailRow(table, "Lighting", item.Illuminated);
                                        AddDetailRow(table, "Traffic", item.TrafficCount);
                                        AddDetailRow(table, "Site ref", item.SiteNumber);
                                        AddDetailRow(table, "Creative notes", item.ItemNotes);
                                    });

                                    if (item.SelectionReasons.Count > 0)
                                    {
                                        itemCol.Item().Text($"Why selected: {ToClientCopy(string.Join(" | ", item.SelectionReasons))}").FontColor("#4B5563");
                                    }

                                });
                            }
                        });
                    }
                });
            });
        }).GeneratePdf();
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

    private static string FormatCurrency(decimal amount)
    {
        return $"R {amount.ToString("N2", CultureInfo.GetCultureInfo("en-ZA"))}";
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

        return Regex.Replace(value, "\\booh\\b", "Billboards and Digital Screens", RegexOptions.IgnoreCase);
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
}

internal sealed class RecommendationDocumentModel
{
    public string ClientName { get; init; } = string.Empty;
    public string? BusinessName { get; init; }
    public string CampaignName { get; init; } = string.Empty;
    public string? CampaignApprovalsUrl { get; init; }
    public string PackageName { get; init; } = string.Empty;
    public decimal SelectedBudget { get; init; }
    public DateTime GeneratedAtUtc { get; init; }
    public string? CampaignObjective { get; init; }
    public string? SpecialRequirements { get; init; }
    public IReadOnlyList<string> TargetAreas { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> TargetLanguages { get; init; } = Array.Empty<string>();
    public IReadOnlyList<RecommendationProposalDocumentModel> Proposals { get; init; } = Array.Empty<RecommendationProposalDocumentModel>();
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
    public int Quantity { get; init; }
    public string? Region { get; init; }
    public string? Language { get; init; }
    public string? TimeBand { get; init; }
    public string? SlotType { get; init; }
    public string? Duration { get; init; }
    public string? Flighting { get; init; }
    public string? Restrictions { get; init; }
    public string? Dimensions { get; init; }
    public string? Material { get; init; }
    public string? Illuminated { get; init; }
    public string? TrafficCount { get; init; }
    public string? SiteNumber { get; init; }
    public string? ItemNotes { get; init; }
    public IReadOnlyList<string> SelectionReasons { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> PolicyFlags { get; init; } = Array.Empty<string>();
}

