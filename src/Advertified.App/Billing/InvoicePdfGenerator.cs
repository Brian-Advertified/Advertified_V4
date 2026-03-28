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

    internal static byte[] Generate(InvoiceIssuerProfile issuer, Invoice invoice, string? logoPath)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        var lineItems = GetEffectiveLineItems(invoice);
        var subtotal = lineItems.Sum(x => x.SubtotalAmount);
        var vatAmount = lineItems.Sum(x => x.VatAmount);

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
                            DataCell(table, item.Quantity.ToString("N2", CultureInfo.GetCultureInfo("en-ZA")), true);
                            DataCell(table, FormatCurrency(item.TotalAmount), true);
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

    private static IReadOnlyList<InvoiceLineItem> GetEffectiveLineItems(Invoice invoice)
    {
        if (invoice.LineItems.Count > 0)
        {
            return invoice.LineItems.OrderBy(x => x.SortOrder).ThenBy(x => x.CreatedAtUtc).ToList();
        }

        var subtotal = Math.Round(invoice.TotalAmount / (1m + VatRate), 2, MidpointRounding.AwayFromZero);
        var vatAmount = Math.Round(invoice.TotalAmount - subtotal, 2, MidpointRounding.AwayFromZero);
        var packageLabel = string.IsNullOrWhiteSpace(invoice.PackageName) ? "Campaign package" : invoice.PackageName.Trim();
        var campaignLabel = string.IsNullOrWhiteSpace(invoice.CampaignName) ? "Advertified campaign" : invoice.CampaignName.Trim();

        return new List<InvoiceLineItem>
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
    }
}
