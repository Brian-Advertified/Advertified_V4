namespace Advertified.App.Data.Entities;

public sealed class Invoice
{
    public Guid Id { get; set; }
    public Guid PackageOrderId { get; set; }
    public Guid? CampaignId { get; set; }
    public Guid UserId { get; set; }
    public Guid? CompanyId { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string InvoiceType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Currency { get; set; } = "ZAR";
    public decimal TotalAmount { get; set; }
    public string CampaignName { get; set; } = string.Empty;
    public string? PackageName { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerEmail { get; set; } = string.Empty;
    public string CustomerAddress { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string? CompanyRegistrationNumber { get; set; }
    public string? CompanyVatNumber { get; set; }
    public string? CompanyAddress { get; set; }
    public string? PaymentReference { get; set; }
    public string? StorageObjectKey { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? DueAtUtc { get; set; }
    public DateTime? PaidAtUtc { get; set; }

    public PackageOrder PackageOrder { get; set; } = null!;
    public Campaign? Campaign { get; set; }
    public UserAccount User { get; set; } = null!;
    public BusinessProfile? Company { get; set; }
    public ICollection<InvoiceLineItem> LineItems { get; set; } = new List<InvoiceLineItem>();
}
