using System.Globalization;
using Advertified.App.Data.Entities;
using Advertified.App.Support;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Advertified.App.Billing;

internal static class InvoicePdfGenerator
{
    private const decimal VatRate = 0.15m;
    private const int ProcessingTimelineBusinessDays = 5;
    private const string TermsUrl = "https://advertified.com/terms-of-service";

    internal static byte[] Generate(InvoiceIssuerProfile issuer, Invoice invoice, string? logoPath)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        var lineItems = GetEffectiveLineItems(invoice);
        var subtotal = lineItems.Sum(x => x.SubtotalAmount);
        var vatAmount = lineItems.Sum(x => x.VatAmount);
        var placeholders = BuildPlaceholders(issuer, invoice);

        return Document.Create(document =>
        {
            document.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(36);
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
                    });

                    row.ConstantItem(220).AlignRight().Column(col =>
                    {
                        col.Item().Text(invoice.Status == InvoiceStatuses.Paid ? "TAX INVOICE" : "INVOICE").SemiBold().FontSize(24);
                        col.Item().Text($"Invoice: {invoice.InvoiceNumber}").SemiBold();
                        col.Item().Text(invoice.Status == InvoiceStatuses.Paid
                            ? $"Paid: {invoice.PaidAtUtc:dd MMM yyyy HH:mm} UTC"
                            : $"Issued: {invoice.CreatedAtUtc:dd MMM yyyy HH:mm} UTC").FontColor("#4B5563");
                        col.Item().PaddingTop(10).AlignRight().Element(container =>
                        {
                            container
                                .Border(2)
                                .BorderColor(GetStampColor(invoice))
                                .Background(GetStampBackground(invoice))
                                .PaddingVertical(6)
                                .PaddingHorizontal(14)
                                .Text(GetStatusStamp(invoice))
                                .SemiBold()
                                .FontSize(18)
                                .FontColor(GetStampColor(invoice));
                        });
                    });
                });

                page.Content().Column(column =>
                {
                    column.Spacing(12);

                    column.Item().Row(row =>
                    {
                        row.RelativeItem().Border(1).BorderColor("#D1D5DB").Padding(10).Column(from =>
                        {
                            from.Item().Text("FROM").FontSize(8).FontColor("#4B5563").SemiBold();
                            from.Item().PaddingTop(4).Text(issuer.LegalName).SemiBold();
                            from.Item().Text(issuer.Address).FontColor("#4B5563");
                            from.Item().Text($"Reg no: {issuer.RegistrationNumber}").FontColor("#4B5563");
                            from.Item().Text($"VAT no: {issuer.VatNumber}").FontColor("#4B5563");
                            from.Item().Text(issuer.ContactEmail).FontColor("#4B5563");
                            from.Item().Text(issuer.ContactPhone).FontColor("#4B5563");
                        });

                        row.RelativeItem().Border(1).BorderColor("#D1D5DB").Padding(10).Column(to =>
                        {
                            to.Item().Text("BILL TO").FontSize(8).FontColor("#4B5563").SemiBold();
                            to.Item().PaddingTop(4).Text(invoice.CustomerName).SemiBold();
                            to.Item().Text(invoice.CustomerAddress).FontColor("#4B5563");
                            to.Item().Text(invoice.CustomerEmail).FontColor("#4B5563");
                            to.Item().Text(invoice.CompanyName).FontColor("#4B5563");
                            to.Item().Text($"Reg no: {invoice.CompanyRegistrationNumber ?? "-"}").FontColor("#4B5563");
                            to.Item().Text($"VAT no: {invoice.CompanyVatNumber ?? "-"}").FontColor("#4B5563");
                        });
                    });

                    column.Item().Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.RelativeColumn();
                            cols.ConstantColumn(70);
                            cols.ConstantColumn(110);
                        });

                        table.Header(header =>
                        {
                            HeaderCell(header, "DESCRIPTION");
                            HeaderCell(header, "QTY", true);
                            HeaderCell(header, "TOTAL", true);
                        });

                        foreach (var item in lineItems)
                        {
                            DataCell(table, item.Description, false);
                            DataCell(table, IsStudioIncludedLine(item) ? "Included" : item.Quantity.ToString("N2", CultureInfo.GetCultureInfo("en-ZA")), true);
                            DataCell(table, IsStudioIncludedLine(item) ? string.Empty : CurrencyFormatSupport.FormatZar(item.TotalAmount), true);
                        }
                    });

                    column.Item().AlignRight().Width(280).Column(totals =>
                    {
                        TotalRow(totals, "Subtotal (excl. VAT)", CurrencyFormatSupport.FormatZar(subtotal), false);
                        TotalRow(totals, "VAT (15%)", CurrencyFormatSupport.FormatZar(vatAmount), false);
                        totals.Item().LineHorizontal(2).LineColor("#111111");
                        TotalRow(totals, invoice.Status == InvoiceStatuses.Paid ? "TOTAL PAID" : "TOTAL DUE", CurrencyFormatSupport.FormatZar(invoice.TotalAmount), true);
                    });

                    column.Item().Border(1).BorderColor("#D1D5DB").Padding(10).Column(meta =>
                    {
                        meta.Item().Text("PAYMENT DETAILS").FontSize(8).FontColor("#4B5563").SemiBold();
                        meta.Item().PaddingTop(4).Text($"Provider: {invoice.Provider}").FontColor("#4B5563");
                        meta.Item().Text($"Invoice type: {invoice.InvoiceType}").FontColor("#4B5563");
                        meta.Item().Text($"Transaction reference: {invoice.PaymentReference ?? "-"}").FontColor("#4B5563");
                        meta.Item().Text(invoice.Status == InvoiceStatuses.Paid
                            ? $"Paid at: {invoice.PaidAtUtc:dd MMM yyyy HH:mm} UTC"
                            : $"Due at: {invoice.DueAtUtc:dd MMM yyyy}").FontColor("#4B5563");
                    });

                    column.Item().Border(1).BorderColor("#D1D5DB").Padding(10).Column(terms =>
                    {
                        terms.Item().Text("TERMS SUMMARY").FontSize(8).FontColor("#4B5563").SemiBold();
                        var shortTerms = BuildShortTermsSummary(placeholders);
                        for (var index = 0; index < shortTerms.Length; index++)
                        {
                            terms.Item().PaddingTop(4).Text($"{index + 1}. {shortTerms[index]}").FontColor("#374151");
                        }

                        terms.Item().PaddingTop(6).Text("By paying this invoice, the client accepts Advertified terms and applicable supplier terms.")
                            .FontColor("#111827").SemiBold();
                        terms.Item().PaddingTop(6).Text($"Full terms and conditions continue on page 2 of this invoice and online at {TermsUrl}.")
                            .FontColor("#0F766E")
                            .SemiBold();
                    });
                });

                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span("Terms and conditions: ").FontColor("#4B5563");
                    text.Span($"{TermsUrl} | Legal annex on page 2").FontColor("#0F766E").SemiBold();
                });
            });

            document.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(36);
                page.DefaultTextStyle(text => text.FontSize(10).FontColor("#111111").FontFamily("Arial"));

                page.Header().Column(header =>
                {
                    header.Item().Text("LEGAL ANNEX: INVOICE TERMS AND CAMPAIGN EXECUTION CONDITIONS").SemiBold().FontSize(12);
                    header.Item().Text($"Reference invoice: {invoice.InvoiceNumber}").FontColor("#4B5563");
                });

                page.Content().PaddingTop(12).Column(content =>
                {
                    content.Spacing(8);
                    foreach (var clause in BuildLegalAnnexClauses(placeholders))
                    {
                        content.Item().Text(clause.Heading).SemiBold().FontSize(10);
                        content.Item().Text(clause.Body).FontColor("#374151").LineHeight(1.35f);
                    }
                });

                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span("Advertified Terms of Service: ").FontColor("#4B5563");
                    text.Span(TermsUrl).FontColor("#0F766E").SemiBold();
                });
            });
        }).GeneratePdf();
    }

    internal static string? ResolveLogoPath(string contentRootPath, string? configuredLogoPath)
    {
        var candidates = new[]
        {
            configuredLogoPath,
            Path.Combine(contentRootPath, "..", "Advertified.Web", "src", "assets", "advertified-logo.png"),
            Path.Combine(contentRootPath, "public", "images", "advertified-logo.png")
        };

        return candidates
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => Path.GetFullPath(path!))
            .FirstOrDefault(File.Exists);
    }

    private static void HeaderCell(TableCellDescriptor header, string text, bool right = false)
    {
        header.Cell().Background("#111111").Padding(8).Element(cell =>
        {
            var content = right ? cell.AlignRight() : cell;
            content.Text(text).FontColor(Colors.White).FontSize(9).SemiBold();
        });
    }

    private static void DataCell(TableDescriptor table, string text, bool right)
    {
        table.Cell().Background("#F3F4F6").Padding(8).Element(cell =>
        {
            var content = right ? cell.AlignRight() : cell;
            content.Text(text);
        });
    }

    private static void TotalRow(ColumnDescriptor column, string label, string value, bool highlight)
    {
        column.Item().Row(row =>
        {
            var labelText = row.RelativeItem().PaddingVertical(4).Text(label);
            var valueText = row.ConstantItem(130).AlignRight().PaddingVertical(4).Text(value);

            if (highlight)
            {
                labelText.SemiBold();
                valueText.SemiBold().FontSize(12);
            }
            else
            {
                labelText.FontColor("#4B5563");
                valueText.SemiBold();
            }
        });
    }

    private static string GetStatusStamp(Invoice invoice)
    {
        return invoice.Status == InvoiceStatuses.Paid ? "PAID" : "UNPAID";
    }

    private static string GetStampColor(Invoice invoice)
    {
        return invoice.Status == InvoiceStatuses.Paid ? "#15803D" : "#B91C1C";
    }

    private static string GetStampBackground(Invoice invoice)
    {
        return invoice.Status == InvoiceStatuses.Paid ? "#F0FDF4" : "#FEF2F2";
    }

    private static IReadOnlyList<InvoiceLineItem> GetEffectiveLineItems(Invoice invoice)
    {
        if (invoice.LineItems.Count > 0)
        {
            var existing = invoice.LineItems.OrderBy(x => x.SortOrder).ThenBy(x => x.CreatedAtUtc).ToList();
            if (ShouldIncludeAiStudioLine(invoice) && !existing.Any(IsStudioIncludedLine))
            {
                existing.Add(CreateAiStudioIncludedLine(invoice, existing.Count));
            }

            return existing;
        }

        var subtotal = Math.Round(invoice.TotalAmount / (1m + VatRate), 2, MidpointRounding.AwayFromZero);
        var vatAmount = Math.Round(invoice.TotalAmount - subtotal, 2, MidpointRounding.AwayFromZero);
        var packageLabel = string.IsNullOrWhiteSpace(invoice.PackageName) ? "Campaign package" : invoice.PackageName.Trim();
        var campaignLabel = string.IsNullOrWhiteSpace(invoice.CampaignName) ? "Advertified campaign" : invoice.CampaignName.Trim();

        var lineItems = new List<InvoiceLineItem>
        {
            new InvoiceLineItem
            {
                Id = Guid.Empty,
                InvoiceId = invoice.Id,
                LineType = "campaign_package",
                Description = $"{packageLabel} ({campaignLabel})",
                Quantity = 1m,
                UnitAmount = invoice.TotalAmount,
                SubtotalAmount = subtotal,
                VatAmount = vatAmount,
                TotalAmount = invoice.TotalAmount,
                SortOrder = 0,
                CreatedAtUtc = invoice.CreatedAtUtc
            }
        };

        if (ShouldIncludeAiStudioLine(invoice))
        {
            lineItems.Add(CreateAiStudioIncludedLine(invoice, lineItems.Count));
        }

        return lineItems;
    }

    private static bool ShouldIncludeAiStudioLine(Invoice invoice)
    {
        return PricingPolicy.IncludesAiCreative(null, invoice.PackageName);
    }

    private static bool IsStudioIncludedLine(InvoiceLineItem item)
    {
        return string.Equals(item.LineType, "ai_studio_included", StringComparison.OrdinalIgnoreCase);
    }

    private static InvoiceLineItem CreateAiStudioIncludedLine(Invoice invoice, int sortOrder)
    {
        return new InvoiceLineItem
        {
            Id = Guid.Empty,
            InvoiceId = invoice.Id,
            LineType = "ai_studio_included",
            Description = "AI Studio services",
            Quantity = 1m,
            UnitAmount = 0m,
            SubtotalAmount = 0m,
            VatAmount = 0m,
            TotalAmount = 0m,
            SortOrder = sortOrder,
            CreatedAtUtc = invoice.CreatedAtUtc
        };
    }

    private static Dictionary<string, string> BuildPlaceholders(InvoiceIssuerProfile issuer, Invoice invoice)
    {
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["InvoiceNumber"] = invoice.InvoiceNumber,
            ["CampaignName"] = string.IsNullOrWhiteSpace(invoice.CampaignName) ? "Campaign" : invoice.CampaignName.Trim(),
            ["PackageName"] = string.IsNullOrWhiteSpace(invoice.PackageName) ? "Package" : invoice.PackageName.Trim(),
            ["Provider"] = string.IsNullOrWhiteSpace(invoice.Provider) ? "Payment provider" : invoice.Provider.Trim(),
            ["IssuerName"] = string.IsNullOrWhiteSpace(issuer.LegalName) ? "Advertified" : issuer.LegalName.Trim(),
            ["ClientName"] = string.IsNullOrWhiteSpace(invoice.CustomerName) ? "Client" : invoice.CustomerName.Trim(),
            ["Amount"] = CurrencyFormatSupport.FormatZar(invoice.TotalAmount),
            ["ProcessingTimelineBusinessDays"] = ProcessingTimelineBusinessDays.ToString(CultureInfo.InvariantCulture),
            ["TermsUrl"] = TermsUrl,
        };
    }

    private static string ResolveTemplate(string template, IReadOnlyDictionary<string, string> placeholders)
    {
        var output = template;
        foreach (var entry in placeholders)
        {
            output = output.Replace($"{{{entry.Key}}}", entry.Value, StringComparison.OrdinalIgnoreCase);
        }

        return output;
    }

    private static string[] BuildShortTermsSummary(IReadOnlyDictionary<string, string> placeholders)
    {
        var templates = new[]
        {
            "Payment is due within 7 days from invoice date unless otherwise agreed in writing.",
            "Late payments incur interest at 2% per month, calculated daily.",
            "Campaigns may be suspended after 7 overdue days and cancelled after 14 overdue days.",
            "Media placements remain subject to supplier availability and confirmation.",
            "No booking is secured until payment or valid proof of payment is received.",
            "Advertified may substitute equivalent media placements where necessary.",
            "Refunds are not standard and remain subject to supplier approval.",
            "Advertified's liability is limited to fees paid by the client."
        };

        return templates.Select(template => ResolveTemplate(template, placeholders)).ToArray();
    }

    private static (string Heading, string Body)[] BuildLegalAnnexClauses(IReadOnlyDictionary<string, string> placeholders)
    {
        var clauses = new (string Heading, string Body)[]
        {
            ("1. Agreement Formation",
                "These terms form part of the binding agreement between {IssuerName} and the client upon written acceptance of a quotation or proposal, issue of a purchase order or instruction, or payment of invoice {InvoiceNumber}. Where conflict exists, precedence is: signed agreement, approved proposal or insertion order, then these terms."),
            ("2. Payment Terms",
                "Payment is due within 7 days from invoice date unless otherwise agreed in writing. Late payments incur interest at 2% per month, calculated daily. Advertified may suspend campaigns overdue by more than 7 days and cancel campaigns overdue by more than 14 days. The client remains liable for reasonable legal and collection costs."),
            ("3. Booking and Media Placement",
                "All placements remain subject to supplier availability and confirmation. No booking is secured until payment or valid proof of payment is received. Advertified may substitute equivalent media placements where necessary."),
            ("4. Cancellations and Amendments",
                "Cancellations must be submitted in writing. Cancellation fees may be up to 50% more than 14 days before campaign start and up to 100% less than 7 days before campaign start. Post-confirmation changes may incur additional costs and require supplier approval."),
            ("5. Third-Party Media Suppliers",
                "{IssuerName} acts as an intermediary only. Supplier terms apply in addition to these terms. Advertified is not liable for supplier delays, errors, or non-performance. In the event of supplier failure, Advertified's obligation is limited to rebooking equivalent media or issuing credit where applicable."),
            ("6. Campaign Execution",
                "Campaign timelines depend on receipt of payment, final creative approval, and supplier scheduling. Delays caused by the client do not entitle the client to refunds."),
            ("7. Creative Content and Compliance",
                "The client warrants that all content complies with South African law and Advertising Regulatory Board standards. Advertified may reject non-compliant material."),
            ("8. Intellectual Property and Data Protection",
                "The client retains ownership of supplied creative assets and grants Advertified a non-exclusive license to use campaign materials for execution and marketing purposes. The client indemnifies Advertified against intellectual property claims. Personal information is processed in accordance with POPIA for campaign execution and communication."),
            ("9. Proof of Performance and No Guarantee",
                "Proof of execution may include photos, logs, or supplier reports, depending on supplier capability, and such proof is sufficient evidence of delivery. Advertified does not guarantee sales outcomes, audience engagement, or ROI, and advertising inherently carries commercial risk."),
            ("10. Refunds, Liability, and Indemnity",
                "Refunds are not standard and remain subject to supplier approval. Where applicable, refunds are issued as account credit by default or partial monetary refund at Advertified's discretion. Advertified's total liability is limited to fees paid by the client and excludes indirect or consequential losses. The client indemnifies Advertified against claims arising from illegal or non-compliant advertising content, intellectual property infringement, defamation, or regulatory breaches."),
            ("11. Force Majeure, Disputes, and Governing Law",
                "Advertified is not liable for delays or failures caused by events beyond its control, including natural disasters, government actions, or supplier disruptions. Disputes must be submitted in writing within 5 business days, and the parties must attempt good-faith resolution before litigation. These terms are governed by the laws of the Republic of South Africa, with jurisdiction in the Gauteng High Court. The online terms remain available at {TermsUrl}.")
        };

        return clauses
            .Select(clause => (
                Heading: clause.Heading,
                Body: ResolveTemplate(clause.Body, placeholders)))
            .ToArray();
    }
}
