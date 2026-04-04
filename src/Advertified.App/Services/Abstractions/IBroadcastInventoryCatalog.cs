using System.Text.Json;
using System.Text.Json.Serialization;

namespace Advertified.App.Services.Abstractions;

public interface IBroadcastInventoryCatalog
{
    Task<IReadOnlyList<BroadcastInventoryRecord>> GetRecordsAsync(CancellationToken cancellationToken);
    Task RefreshAsync(CancellationToken cancellationToken);
}

public sealed class BroadcastInventoryRecord
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("station")]
    public string Station { get; set; } = string.Empty;

    [JsonPropertyName("media_type")]
    public string MediaType { get; set; } = string.Empty;

    [JsonPropertyName("catalog_health")]
    public string CatalogHealth { get; set; } = string.Empty;

    [JsonPropertyName("coverage_type")]
    public string CoverageType { get; set; } = string.Empty;

    [JsonPropertyName("broadcast_frequency")]
    public string? BroadcastFrequency { get; set; }

    [JsonPropertyName("province_codes")]
    public List<string> ProvinceCodes { get; set; } = new();

    [JsonPropertyName("city_labels")]
    public List<string> CityLabels { get; set; } = new();

    [JsonPropertyName("primary_languages")]
    public List<string> PrimaryLanguages { get; set; } = new();

    [JsonPropertyName("language_display")]
    public string? LanguageDisplay { get; set; }

    [JsonPropertyName("language_notes")]
    public string? LanguageNotes { get; set; }

    [JsonPropertyName("listenership_daily")]
    public long? ListenershipDaily { get; set; }

    [JsonPropertyName("listenership_weekly")]
    public long? ListenershipWeekly { get; set; }

    [JsonPropertyName("listenership_period")]
    public string? ListenershipPeriod { get; set; }

    [JsonPropertyName("audience_age_skew")]
    public string? AudienceAgeSkew { get; set; }

    [JsonPropertyName("audience_gender_skew")]
    public string? AudienceGenderSkew { get; set; }

    [JsonPropertyName("audience_lsm_range")]
    public string? AudienceLsmRange { get; set; }

    [JsonPropertyName("audience_racial_skew")]
    public string? AudienceRacialSkew { get; set; }

    [JsonPropertyName("urban_rural_mix")]
    public string? UrbanRuralMix { get; set; }

    [JsonPropertyName("target_audience")]
    public string? TargetAudience { get; set; }

    [JsonPropertyName("audience_keywords")]
    public List<string> AudienceKeywords { get; set; } = new();

    [JsonPropertyName("packages")]
    public JsonElement Packages { get; set; }

    [JsonPropertyName("pricing")]
    public JsonElement Pricing { get; set; }

    [JsonPropertyName("data_source_enrichment")]
    public JsonElement DataSourceEnrichment { get; set; }

    [JsonPropertyName("has_pricing")]
    public bool HasPricing { get; set; }

    [JsonPropertyName("is_national")]
    public bool IsNational { get; set; }
}
