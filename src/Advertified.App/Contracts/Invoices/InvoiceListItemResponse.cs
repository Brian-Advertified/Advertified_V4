namespace Advertified.App.Contracts.Invoices;

public sealed record InvoiceListItemResponse(
    Guid Id,
    string InvoiceNumber,
    string CampaignName,
    string? PackageName,
    string Provider,
    string InvoiceType,
    string Status,
    decimal TotalAmount,
    string Currency,
    DateTime CreatedAtUtc,
    DateTime? PaidAtUtc,
    string? PaymentReference,
    string PdfUrl);
