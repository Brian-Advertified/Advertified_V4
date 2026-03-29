namespace Advertified.App.Contracts.Public;

public sealed class PartnerEnquiryRequest
{
    public string FullName { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string PartnerType { get; set; } = string.Empty;
    public string? InventorySummary { get; set; }
    public string Message { get; set; } = string.Empty;
}
