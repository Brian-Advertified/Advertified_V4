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
                            DataCell(table, IsStudioIncludedLine(item) ? string.Empty : FormatCurrency(item.TotalAmount), true);
                        }
                    });

                    column.Item().AlignRight().Width(280).Column(totals =>
                    {
                        TotalRow(totals, "Subtotal (excl. VAT)", FormatCurrency(subtotal), false);
                        TotalRow(totals, "VAT (15%)", FormatCurrency(vatAmount), false);
                        totals.Item().LineHorizontal(2).LineColor("#111111");
                        TotalRow(totals, invoice.Status == InvoiceStatuses.Paid ? "TOTAL PAID" : "TOTAL DUE", FormatCurrency(invoice.TotalAmount), true);
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
            Path.Combine(contentRootPath, "..", "Advertified.Web", "src", "assets", "advertified-logo-v3.png"),
            Path.Combine(contentRootPath, "public", "images", "advertified-logo-v3.png")
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

    private static string FormatCurrency(decimal amount)
    {
        return $"R {amount.ToString("N2", CultureInfo.GetCultureInfo("en-ZA"))}";
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
            ["Amount"] = FormatCurrency(invoice.TotalAmount),
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
            "Campaign onboarding and supplier booking may take up to {ProcessingTimelineBusinessDays} business days after cleared payment and receipt of required materials.",
            "Media inventory is subject to final supplier availability at booking time.",
            "If selected inventory is unavailable, {IssuerName} may substitute equivalent inventory of similar value/reach, with client notice.",
            "Package value remains fixed at {Amount}; channel mix and line items may vary within package policy and availability.",
            "Campaign activation begins after payment confirmation, recommendation approval, and required creative/material delivery.",
            "Processing times may be affected by payment providers, supplier systems, and external platform/webhook behavior.",
            "Refund and cancellation outcomes depend on campaign stage and supplier commitments; booked media and third-party fees may be non-refundable.",
            "Supplier booking, artwork, cancellation, and make-good terms are incorporated by reference and apply per line item."
        };

        return templates.Select(template => ResolveTemplate(template, placeholders)).ToArray();
    }

    private static (string Heading, string Body)[] BuildLegalAnnexClauses(IReadOnlyDictionary<string, string> placeholders)
    {
        var clauses = new (string Heading, string Body)[]
        {
            ("1. Definitions",
                "\"Issuer\" means {IssuerName}. \"Client\" means the invoiced party. \"Campaign\" means the services and media execution linked to invoice {InvoiceNumber}. \"Inventory\" means media placements or equivalent purchasable media units. \"Supplier\" means third-party media owners, stations, platforms, and partners."),
            ("2. Acceptance and Scope",
                "Payment of this invoice confirms acceptance of these terms. This invoice covers package value and campaign services captured in the order records for {CampaignName} under package {PackageName}. Supplier-specific terms applicable to booked inventory are incorporated by reference. The current online terms are available at {TermsUrl}."),
            ("3. Processing Timeline",
                "Campaign onboarding and booking operations may take up to {ProcessingTimelineBusinessDays} business days from the later of cleared payment and receipt of all required campaign inputs/materials. Time estimates are targets, not guarantees, where third-party dependencies apply."),
            ("4. Payment and Package Value",
                "Package pricing remains fixed at the paid invoice amount ({Amount}) unless explicitly amended in writing. Budget allocation and channel mix may change during planning and optimization while preserving package value and policy constraints."),
            ("5. Inventory Availability and Substitution",
                "All inventory is subject to real-time supplier availability at booking. Availability shown during planning/recommendation is indicative until supplier acceptance. If selected inventory becomes unavailable, issuer may substitute inventory of comparable value, fit, and delivery intent, with notice where practicable."),
            ("6. Activation Preconditions",
                "Campaign activation requires payment confirmation, recommendation approval where applicable, and compliant final creative/assets. Delays in approvals, briefing, or material delivery may shift start dates and delivery windows."),
            ("7. Supplier and Third-Party Dependencies",
                "Processing and execution may be affected by supplier cutoffs, booking queues, technical outages, and external integration/webhook timing. Issuer is not liable for delays caused by systems outside reasonable control."),
            ("8. Payment Provider Terms",
                "Outcomes for provider flows, including approval, decline, reversal, cancellation, timeout, or reconciliation, are governed by provider rules. Callback/webhook behavior and timing are provider-dependent."),
            ("9. Recommendation and Performance",
                "Recommendations, projected reach, and estimated outcomes are planning tools and not guaranteed performance commitments. Actual results may vary with market, audience, and platform conditions."),
            ("10. Proof, Reporting, and Documentation",
                "Proof of booking, delivery updates, and reporting are supplied as available from supplier and system records. Reporting cadence and granularity may vary by channel and supplier capability."),
            ("11. Cancellations, Changes, and Refunds",
                "Refund eligibility depends on campaign stage and supplier commitments. Once bookings are placed or media has begun, portions may be non-refundable. Non-recoverable third-party costs and applicable retained gateway/provider fees may be deducted."),
            ("12. Compliance and Materials",
                "Client warrants it has rights to submitted materials and claims, and that content complies with law, platform policy, and supplier standards. Issuer may reject or request amendment of non-compliant content."),
            ("13. Limitation of Liability",
                "To the maximum extent permitted by law, issuer liability is limited to amounts paid under this invoice. Issuer is not liable for indirect or consequential damages arising from third-party media delivery/platform behavior."),
            ("14. Terms Hierarchy and Notices",
                "Where conflict exists, precedence is: signed master agreement (if any), this invoice and annex, then channel/supplier-specific booking terms. Operational notices may be sent via email and in-platform status updates.")
        };

        return clauses
            .Select(clause => (
                Heading: clause.Heading,
                Body: ResolveTemplate(clause.Body, placeholders)))
            .ToArray();
    }
}
