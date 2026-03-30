namespace Advertified.App.Contracts.Admin;

public sealed class UpdateAdminRateCardRequest
{
    public string Channel { get; set; } = string.Empty;
    public string? SupplierOrStation { get; set; }
    public string? DocumentTitle { get; set; }
    public string? Notes { get; set; }
}
