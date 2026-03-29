namespace Advertified.App.Contracts.Admin;

public sealed class AdminRateCardUploadResponse
{
    public string SourceFile { get; set; } = string.Empty;
    public string StoredFileName { get; set; } = string.Empty;
    public string Channel { get; set; } = string.Empty;
    public string? SupplierOrStation { get; set; }
    public string? DocumentTitle { get; set; }
    public DateTime ImportedAt { get; set; }
}
