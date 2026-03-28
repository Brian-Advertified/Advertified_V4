namespace Advertified.App.Domain.Accounts;

public sealed class BusinessProfile
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string BusinessName { get; set; } = string.Empty;
    public string BusinessType { get; set; } = string.Empty;
    public string RegistrationNumber { get; set; } = string.Empty;
    public string? VatNumber { get; set; }
    public string Industry { get; set; } = string.Empty;
    public string AnnualRevenueBand { get; set; } = string.Empty;
    public string? TradingAsName { get; set; }
    public string StreetAddress { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Province { get; set; } = string.Empty;
    public string VerificationStatus { get; set; } = "not_submitted";
}
