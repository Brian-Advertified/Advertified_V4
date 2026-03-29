namespace Advertified.App.Contracts.Admin;

public sealed class AdminOutletDetailResponse
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string MediaType { get; set; } = string.Empty;
    public string CoverageType { get; set; } = string.Empty;
    public string CatalogHealth { get; set; } = string.Empty;
    public string? OperatorName { get; set; }
    public bool IsNational { get; set; }
    public bool HasPricing { get; set; }
    public string? LanguageNotes { get; set; }
    public string? TargetAudience { get; set; }
    public string? BroadcastFrequency { get; set; }
    public IReadOnlyList<string> PrimaryLanguages { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> ProvinceCodes { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> CityLabels { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> AudienceKeywords { get; set; } = Array.Empty<string>();
    public int PackageCount { get; set; }
    public int SlotRateCount { get; set; }
    public decimal? MinPackagePrice { get; set; }
    public decimal? MinSlotRate { get; set; }
}
