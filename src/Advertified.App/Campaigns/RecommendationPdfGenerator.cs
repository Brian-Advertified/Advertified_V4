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

                        col.Item()
                            .PaddingTop(6)
                            .Text(model.OpportunityContext?.IsLeadOutreach == true ? "Growth Opportunity Recommendation Pack" : "Recommendation Pack")
                            .SemiBold()
                            .FontSize(20);
                        col.Item().Text(model.CampaignName).FontColor("#4B5563");
                    });

                    row.ConstantItem(220).AlignRight().Column(col =>
                    {
                        col.Item().Text("CLIENT COPY").SemiBold().FontSize(22);
                        col.Item().Text($"Generated: {model.GeneratedAtUtc:dd MMM yyyy HH:mm} UTC").FontColor("#4B5563");
                        col.Item().Text($"Package: {model.PackageName}").FontColor("#4B5563");
                        col.Item().Text($"{model.BudgetLabel}: {model.BudgetDisplayText}").SemiBold();
                    });
                });

                page.Content().Column(column =>
                {
                    column.Spacing(12);

                    if (model.OpportunityContext?.IsLeadOutreach == true)
                    {
                        column.Item().Border(1).BorderColor("#C7E0D6").Background("#F3FBF7").Padding(12).Column(opportunity =>
                        {
                            opportunity.Spacing(6);
                            opportunity.Item().Text("Growth Opportunity Snapshot").SemiBold().FontSize(12);
                            opportunity.Item().Text(
                                $"We found where {ResolveBusinessReference(model)} is losing customers, and built the campaign to fix it.");

                            if (!string.IsNullOrWhiteSpace(model.OpportunityContext.ArchetypeName))
                            {
                                opportunity.Item()
                                    .Text($"Growth pattern: {ToClientCopy(model.OpportunityContext.ArchetypeName)}")
                                    .FontColor("#4B5563");
                            }

                            if (!string.IsNullOrWhiteSpace(model.OpportunityContext.WhoWeAre))
                            {
                                opportunity.Item().Text("Who we are").SemiBold();
                                opportunity.Item().Text(TruncateClientCopy(model.OpportunityContext.WhoWeAre, 220)).FontColor("#4B5563");
                            }

                            if (!string.IsNullOrWhiteSpace(model.OpportunityContext.LastResearchedAtUtc))
                            {
                                opportunity.Item().Text($"Last researched: {ToClientCopy(model.OpportunityContext.LastResearchedAtUtc)}").FontColor("#4B5563");
                            }

                            var topGaps = model.OpportunityContext.DetectedGaps
                                .Where(static gap => !string.IsNullOrWhiteSpace(gap))
                                .Take(3)
                                .ToArray();
                            if (topGaps.Length > 0)
                            {
                                opportunity.Item().Text("Where growth can be unlocked").SemiBold();
                                foreach (var gap in topGaps)
                                {
                                    opportunity.Item().Text($"- {TruncateClientCopy(gap, 150)}").FontColor("#4B5563");
                                }
                            }

                            if (!string.IsNullOrWhiteSpace(model.OpportunityContext.ExpectedOutcome))
                            {
                                opportunity.Item().Text("Expected outcome").SemiBold();
                                opportunity.Item()
                                    .Text(TruncateClientCopy(model.OpportunityContext.ExpectedOutcome, 220))
                                    .FontColor("#4B5563");
                            }

                            if (!string.IsNullOrWhiteSpace(model.OpportunityContext.WhyActNow))
                            {
                                opportunity.Item().Text("Why timing matters").SemiBold();
                                opportunity.Item().Text(TruncateClientCopy(model.OpportunityContext.WhyActNow, 180)).FontColor("#4B5563");
                            }

                            if (!string.IsNullOrWhiteSpace(model.OpportunityContext.FlexibleRollout))
                            {
                                opportunity.Item().Text("Flexible rollout").SemiBold();
                                opportunity.Item().Text(TruncateClientCopy(model.OpportunityContext.FlexibleRollout, 180)).FontColor("#4B5563");
                            }

                            if (!string.IsNullOrWhiteSpace(model.OpportunityContext.NextStep))
                            {
                                opportunity.Item().Text("Next step").SemiBold();
                                opportunity.Item().Text(TruncateClientCopy(model.OpportunityContext.NextStep, 180)).FontColor("#4B5563");
                            }
                        });
                    }

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

                        if (!string.IsNullOrWhiteSpace(model.TargetAudienceSummary))
                        {
                            summary.Item().Text($"Target audience: {ToClientCopy(model.TargetAudienceSummary)}").FontColor("#4B5563");
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
                            proposalSummary.Item().Text("Recommended Growth Paths").SemiBold().FontSize(12);

                            foreach (var proposal in model.Proposals)
                            {
                                proposalSummary.Item().Row(row =>
                                {
                                    row.RelativeItem().Text($"{proposal.Label} | {ToClientCopy(proposal.Strategy ?? "Recommendation option")}");
                                    row.ConstantItem(150).AlignRight().Text(FormatCurrency(proposal.TotalCost)).SemiBold();
                                });

                                var mediaCounts = BuildProposalMediaCounts(proposal);
                                if (mediaCounts.Count > 0)
                                {
                                    proposalSummary.Item().Table(table =>
                                    {
                                        table.ColumnsDefinition(columns =>
                                        {
                                            columns.RelativeColumn();
                                            columns.ConstantColumn(80);
                                        });

                                        foreach (var count in mediaCounts)
                                        {
                                            table.Cell().PaddingVertical(2).Text(count.Channel).FontColor("#4B5563");
                                            table.Cell().PaddingVertical(2).AlignRight().Text(count.Quantity.ToString()).SemiBold();
                                        }
                                    });
                                }

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

                                    var clientSelectionSummary = BuildClientSelectionSummary(model, item);
                                    if (clientSelectionSummary.Count > 0)
                                    {
                                        itemCol.Item().Column(details =>
                                        {
                                            details.Spacing(2);
                                            foreach (var line in clientSelectionSummary)
                                            {
                                                details.Item().Text(line).FontColor("#4B5563");
                                            }
                                        });
                                    }

                                });
                            }
                        });
                    }

                    column.Item().PageBreak();
                    column.Item().Border(1).BorderColor("#D1D5DB").Padding(12).Column(terms =>
                    {
                        terms.Spacing(8);
                        terms.Item().Text("Terms and Conditions Summary").SemiBold().FontSize(12);

                        foreach (var clause in BuildTermsSummary())
                        {
                            terms.Item().Text(clause).FontColor("#374151");
                        }

                        terms.Item().PaddingTop(6).Text($"Full terms and conditions are available online at {TermsUrl}.")
                            .FontColor("#0F766E")
                            .SemiBold();
                    });
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

    private static IReadOnlyList<(string Channel, int Quantity)> BuildProposalMediaCounts(RecommendationProposalDocumentModel proposal)
    {
        return proposal.Items
            .Where(item => !string.IsNullOrWhiteSpace(item.Channel))
            .GroupBy(item => FormatChannelLabel(item.Channel))
            .Select(group =>
            {
                var quantity = group.Sum(item => item.Quantity > 0 ? item.Quantity : 1);
                return (Channel: group.Key, Quantity: quantity);
            })
            .OrderBy(item => item.Channel, StringComparer.OrdinalIgnoreCase)
            .ToArray();
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
