namespace Advertified.App.Contracts.Admin;

public sealed class AdminPackageOrderItemResponse
{
    public Guid OrderId { get; set; }
    public Guid? UserId { get; set; }
    public string ClientName { get; set; } = string.Empty;
    public string ClientEmail { get; set; } = string.Empty;
    public string ClientPhone { get; set; } = string.Empty;
    public Guid PackageBandId { get; set; }
    public string PackageBandName { get; set; } = string.Empty;
    public decimal SelectedBudget { get; set; }
    public decimal ChargedAmount { get; set; }
    public string Currency { get; set; } = "ZAR";
    public string PaymentProvider { get; set; } = string.Empty;
    public string PaymentStatus { get; set; } = string.Empty;
    public string? PaymentReference { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? PurchasedAt { get; set; }
    public Guid? CampaignId { get; set; }
    public string? CampaignName { get; set; }
    public Guid? InvoiceId { get; set; }
    public string? InvoiceStatus { get; set; }
    public string? InvoicePdfUrl { get; set; }
    public string? SupportingDocumentPdfUrl { get; set; }
    public string? SupportingDocumentFileName { get; set; }
    public DateTimeOffset? SupportingDocumentUploadedAt { get; set; }
    public bool CanUpdateLulaStatus { get; set; }
}
