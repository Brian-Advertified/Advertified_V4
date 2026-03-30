using System.Text.RegularExpressions;

namespace Advertified.App.Support;

internal static class InvoiceStoragePathBuilder
{
    internal static string BuildInvoiceFolder(string invoiceNumber)
    {
        var normalized = NormalizeSegment(invoiceNumber);
        return $"invoices/{normalized}";
    }

    internal static string BuildInvoicePdfObjectKey(string invoiceNumber)
    {
        var folder = BuildInvoiceFolder(invoiceNumber);
        var normalized = NormalizeSegment(invoiceNumber);
        return $"{folder}/invoice-{normalized}.pdf";
    }

    internal static string BuildSupportingDocumentObjectKey(string invoiceNumber, string fileName)
    {
        var folder = BuildInvoiceFolder(invoiceNumber);
        return $"{folder}/{NormalizeSegment(fileName)}";
    }

    private static string NormalizeSegment(string value)
    {
        var trimmed = value.Trim();
        var safe = Regex.Replace(trimmed, @"[^A-Za-z0-9._-]", "-");
        return string.IsNullOrWhiteSpace(safe) ? "document" : safe;
    }
}
