namespace Advertified.App.Data.Entities;

public sealed class InvoiceLineItem
{
    public Guid Id { get; set; }
    public Guid InvoiceId { get; set; }
    public string LineType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitAmount { get; set; }
    public decimal SubtotalAmount { get; set; }
    public decimal VatAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public int SortOrder { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    public Invoice Invoice { get; set; } = null!;
}
