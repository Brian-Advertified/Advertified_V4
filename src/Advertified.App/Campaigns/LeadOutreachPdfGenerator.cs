using System.Globalization;
using System.Text.RegularExpressions;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Advertified.App.Campaigns;

internal static class LeadOutreachPdfGenerator
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
                page.Margin(28);
                page.DefaultTextStyle(text => text.FontSize(10).FontColor("#0F1A14").FontFamily("Arial"));

                page.Header().PaddingBottom(12).Row(row =>
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

                        col.Item().PaddingTop(4).Text("Growth Opportunity Pack").SemiBold().FontSize(18);
                        col.Item().Text(ToClientCopy(model.CampaignName)).FontColor("#3D4F45");
                    });

                    row.ConstantItem(220).AlignRight().Column(col =>
                    {
                        col.Item().Text("PUBLIC-SIGNAL REVIEW").SemiBold().FontSize(12).FontColor("#0F6E56");
                        col.Item().Text($"Prepared: {model.GeneratedAtUtc:dd MMM yyyy}").FontColor("#3D4F45");
                    });
                });

                page.Content().Column(column =>
                {
                    column.Spacing(10);

                    column.Item().Border(1).BorderColor("#DDE8E3").Background("#F8F5F0").Padding(12).Column(who =>
                    {
                        who.Spacing(5);
                        who.Item().Text("Who We Are").SemiBold().FontSize(11);
                        var intro = !string.IsNullOrWhiteSpace(model.OpportunityContext?.WhoWeAre)
                            ? model.OpportunityContext!.WhoWeAre!
                            : "Advertified helps businesses find where they are losing customers, then launches practical campaigns to fix those gaps.";
                        who.Item().Text(ToClientCopy(intro)).FontColor("#3D4F45");

                        if (!string.IsNullOrWhiteSpace(model.OpportunityContext?.IndustryProfileName))
                        {
                            who.Item()
                                .Text($"Industry profile: {ToClientCopy(model.OpportunityContext.IndustryProfileName)}")
                                .SemiBold()
                                .FontColor("#0F6E56");
                        }

                        if (!string.IsNullOrWhiteSpace(model.OpportunityContext?.IndustryMessagingAngle))
                        {
                            who.Item()
                                .Text($"Messaging angle: {ToClientCopy(model.OpportunityContext.IndustryMessagingAngle)}")
                                .FontColor("#3D4F45");
                        }
                    });

                    column.Item().Border(1).BorderColor("#DDE8E3").Background("#FFFFFF").Padding(14).Column(hook =>
                    {
                        hook.Spacing(7);
                        hook.Item().Text("What We Found").SemiBold().FontSize(12).FontColor("#0F6E56");
                        hook.Item().Text(
                            $"We reviewed {ResolveBusinessReference(model)} and identified specific demand capture gaps that are currently limiting growth.")
                            .SemiBold();

                        var gaps = model.OpportunityContext?.DetectedGaps?
                            .Where(static x => !string.IsNullOrWhiteSpace(x))
                            .Take(3)
                            .ToArray()
                            ?? Array.Empty<string>();
                        if (gaps.Length > 0)
                        {
                            foreach (var gap in gaps)
                            {
                                hook.Item().Text($"- {ToClientCopy(gap)}").FontColor("#3D4F45");
                            }
                        }
                        else if (!string.IsNullOrWhiteSpace(model.OpportunityContext?.LeadInsightSummary))
                        {
                            hook.Item().Text(ToClientCopy(model.OpportunityContext.LeadInsightSummary)).FontColor("#3D4F45");
                        }
                    });

                    column.Item().Border(1).BorderColor("#0F6E56").Background("#0F1A14").Padding(12).Column(bnpl =>
                    {
                        bnpl.Spacing(4);
                        bnpl.Item().Text("Buy Now, Pay Later").SemiBold().FontColor("#FFFFFF").FontSize(11);
                        bnpl.Item().Text("Launch campaigns now and pay from growth instead of upfront cash.").FontColor("#1D9E75");
                    });

                    column.Item().Text("Recommended Growth Paths").SemiBold().FontSize(12);

                    for (var index = 0; index < model.Proposals.Count; index++)
                    {
                        var proposal = model.Proposals[index];
                        var channels = proposal.Items
                            .Where(item => !string.IsNullOrWhiteSpace(item.Channel))
                            .Select(item => FormatChannelLabel(item.Channel))
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .Take(4)
                            .ToArray();

                        column.Item().Border(1).BorderColor(index == 1 ? "#0F6E56" : "#DDE8E3").Background("#FFFFFF").Padding(12).Column(card =>
                        {
                            card.Spacing(5);
                            card.Item().Row(row =>
                            {
                                row.RelativeItem().Text(ToClientCopy(proposal.Label)).SemiBold().FontSize(11);
                                row.ConstantItem(160).AlignRight().Text(FormatCurrency(proposal.TotalCost)).SemiBold();
                            });

                            var outcome = !string.IsNullOrWhiteSpace(proposal.Strategy)
                                ? proposal.Strategy
                                : proposal.Summary;
                            card.Item().Text(ToClientCopy(outcome)).SemiBold();
                            if (!string.IsNullOrWhiteSpace(proposal.Summary))
                            {
                                card.Item().Text(ToClientCopy(proposal.Summary)).FontColor("#3D4F45");
                            }

                            if (channels.Length > 0)
                            {
                                card.Item().Text($"Channels: {string.Join(" | ", channels)}").FontColor("#0F6E56");
                            }

                            if (!string.IsNullOrWhiteSpace(proposal.AcceptUrl))
                            {
                                card.Item().Hyperlink(proposal.AcceptUrl).Text($"Accept {ToClientCopy(proposal.Label)}").SemiBold();
                            }
                        });
                    }

                    column.Item().Border(1).BorderColor("#DDE8E3").Background("#FFFFFF").Padding(12).Column(cta =>
                    {
                        cta.Spacing(5);
                        cta.Item().Text("Next Step").SemiBold().FontSize(11);

                        if (!string.IsNullOrWhiteSpace(model.CampaignApprovalsUrl))
                        {
                            cta.Item().Hyperlink(model.CampaignApprovalsUrl).Text("Explore all options in your review workspace");
                        }

                        cta.Item().Text("Reply to this email and ask for Dev for a quick 15-minute walkthrough.");
                        cta.Item().Text($"Terms: {TermsUrl}").FontColor("#3D4F45");
                    });
                });
            });
        }).GeneratePdf();
    }

    private static string FormatCurrency(decimal amount)
    {
        return $"R {amount.ToString("N2", CultureInfo.GetCultureInfo("en-ZA"))}";
    }

    private static string ResolveBusinessReference(RecommendationDocumentModel model)
    {
        return !string.IsNullOrWhiteSpace(model.BusinessName)
            ? ToClientCopy(model.BusinessName)
            : ToClientCopy(model.ClientName);
    }

    private static string FormatChannelLabel(string? channel)
    {
        if (string.IsNullOrWhiteSpace(channel))
        {
            return string.Empty;
        }

        if (string.Equals(channel.Trim(), "OOH", StringComparison.OrdinalIgnoreCase))
        {
            return "Billboards and Digital Screens";
        }

        return ToClientCopy(channel.Trim());
    }

    private static string ToClientCopy(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value
            .Replace("Ã¢â‚¬â„¢", "'")
            .Replace("Ã¢â‚¬Ëœ", "'")
            .Replace("Ã¢â‚¬Å“", "\"")
            .Replace("Ã¢â‚¬Â", "\"")
            .Replace("Ã¢â‚¬â€œ", "-")
            .Replace("Ã¢â‚¬â€", "-")
            .Replace("\u00A0", " ");
        normalized = Regex.Replace(normalized, "\\s+", " ").Trim();
        return normalized;
    }
}
