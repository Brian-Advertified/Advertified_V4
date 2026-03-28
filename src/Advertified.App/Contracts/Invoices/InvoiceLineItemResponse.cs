namespace Advertified.App.Contracts.Invoices;

public sealed record InvoiceLineItemResponse(
    Guid Id,
    string LineType,
    string Description,
    decimal Quantity,
    decimal UnitAmount,
    decimal SubtotalAmount,
    decimal VatAmount,
    decimal TotalAmount,
    int SortOrder);
