namespace Advertified.App.Data.Entities;

public sealed class LegalDocument
{
    public Guid Id { get; set; }
    public string DocumentKey { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string VersionLabel { get; set; } = string.Empty;
    public string BodyJson { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
