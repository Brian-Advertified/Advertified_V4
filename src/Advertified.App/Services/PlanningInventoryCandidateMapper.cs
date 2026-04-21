using System.Text.Json;
using System.Globalization;
using Advertified.App.Domain.Campaigns;
using Advertified.App.Services.Abstractions;
using Advertified.App.Support;

namespace Advertified.App.Services;

public sealed class PlanningInventoryCandidateMapper : IPlanningInventoryCandidateMapper
{
    private readonly ILeadMasterDataService _leadMasterDataService;

    public PlanningInventoryCandidateMapper(ILeadMasterDataService leadMasterDataService)
    {
        _leadMasterDataService = leadMasterDataService;
    }

    public InventoryCandidate MapOoh(OohPlanningInventoryRow row)
    {
        var metadata = NormalizeMetadata(ParseMetadata(row.MetadataJson), row);
        var parsedGpsCoordinates = GpsCoordinateParser.TryParse(GetMetadataValue(metadata, "gps_coordinates"));
        var latitude = row.Latitude
            ?? ParseNullableDouble(GetMetadataValue(metadata, "latitude"))
            ?? ParseNullableDouble(GetMetadataValue(metadata, "lat"))
            ?? parsedGpsCoordinates?.Latitude;
        var longitude = row.Longitude
            ?? ParseNullableDouble(GetMetadataValue(metadata, "longitude"))
            ?? ParseNullableDouble(GetMetadataValue(metadata, "lng"))
            ?? ParseNullableDouble(GetMetadataValue(metadata, "lon"))
            ?? parsedGpsCoordinates?.Longitude;
        if (latitude.HasValue)
        {
            metadata["latitude"] = latitude.Value;
        }

        if (longitude.HasValue)
        {
            metadata["longitude"] = longitude.Value;
        }

        var nearestLocation = latitude.HasValue && longitude.HasValue
            ? _leadMasterDataService.ResolveNearestLocation(latitude.Value, longitude.Value)
            : null;
        if (nearestLocation is not null)
        {
            var nearestDistanceKm = nearestLocation.DistanceKm;
            SetIfMissing(metadata, "nearestCanonicalLocation", nearestLocation.CanonicalName);
            SetIfMissing(metadata, "nearestLocationType", nearestLocation.LocationType);
            SetIfMissing(metadata, "nearestParentCity", nearestLocation.ParentCity);
            SetIfMissing(metadata, "nearestProvince", nearestLocation.Province);
            SetIfMissing(metadata, "nearestDistanceKm", nearestDistanceKm.HasValue
                ? Math.Round(nearestDistanceKm.Value, 2, MidpointRounding.AwayFromZero)
                : null);
        }

        var resolvedProvince = FirstNonEmpty(row.Province, GetMetadataValue(metadata, "province"), nearestLocation?.Province);
        var resolvedCity = FirstNonEmpty(
            row.City,
            GetMetadataValue(metadata, "city"),
            nearestLocation is not null && string.Equals(nearestLocation.LocationType, "city", StringComparison.OrdinalIgnoreCase) ? nearestLocation.CanonicalName : null,
            nearestLocation?.ParentCity);
        var resolvedArea = FirstNonEmpty(
            row.Area,
            GetMetadataValue(metadata, "area"),
            row.Suburb,
            GetMetadataValue(metadata, "suburb"),
            nearestLocation?.CanonicalName,
            resolvedCity,
            resolvedProvince);

        return new InventoryCandidate
        {
            SourceId = row.SourceId,
            SourceType = row.SourceType,
            DisplayName = row.DisplayName,
            MediaType = row.MediaType,
            Subtype = row.Subtype,
            Province = resolvedProvince,
            City = resolvedCity,
            Suburb = FirstNonEmpty(row.Suburb, GetMetadataValue(metadata, "suburb")),
            Area = resolvedArea,
            Language = FirstNonEmpty(row.Language, GetMetadataValue(metadata, "language")),
            LsmMin = row.LsmMin,
            LsmMax = row.LsmMax,
            Cost = row.Cost,
            IsAvailable = row.IsAvailable,
            PackageOnly = row.PackageOnly,
            TimeBand = FirstNonEmpty(row.TimeBand, GetMetadataValue(metadata, "timeBand")),
            DayType = FirstNonEmpty(row.DayType, GetMetadataValue(metadata, "dayType")),
            SlotType = FirstNonEmpty(row.SlotType, GetMetadataValue(metadata, "slotType")),
            DurationSeconds = row.DurationSeconds ?? ParseNullableInt(GetMetadataValue(metadata, "durationSeconds")),
            RegionClusterCode = FirstNonEmpty(row.RegionClusterCode, GetMetadataValue(metadata, "regionClusterCode")),
            MarketScope = FirstNonEmpty(row.MarketScope, GetMetadataValue(metadata, "marketScope")),
            MarketTier = FirstNonEmpty(row.MarketTier, GetMetadataValue(metadata, "marketTier")),
            MonthlyListenership = row.MonthlyListenership,
            IsFlagshipStation = row.IsFlagshipStation,
            IsPremiumStation = row.IsPremiumStation,
            Latitude = latitude,
            Longitude = longitude,
            Metadata = metadata
        };
    }

    public InventoryCandidate MapBroadcast(BroadcastPlanningInventorySeed seed)
    {
        var record = seed.Record;
        var province = record.ProvinceCodes.FirstOrDefault();
        var city = record.CityLabels.FirstOrDefault();
        var language = record.LanguageDisplay ?? string.Join("/", record.PrimaryLanguages);

        return new InventoryCandidate
        {
            SourceId = seed.SourceId,
            SourceType = seed.SourceType,
            DisplayName = seed.DisplayName,
            MediaType = NormalizeMediaType(record.MediaType),
            Subtype = seed.SlotType,
            Province = province,
            City = city,
            Suburb = null,
            Area = city ?? province ?? record.CoverageType,
            Language = string.IsNullOrWhiteSpace(language) ? null : language,
            LsmMin = null,
            LsmMax = null,
            Cost = seed.Cost,
            IsAvailable = true,
            PackageOnly = seed.SourceType.EndsWith("_package", StringComparison.OrdinalIgnoreCase),
            TimeBand = GetMetadataString(seed.Metadata, "timeBand"),
            DayType = GetMetadataString(seed.Metadata, "dayType"),
            SlotType = seed.SlotType,
            DurationSeconds = TryParseDuration(record.BroadcastFrequency),
            RegionClusterCode = province,
            MarketScope = record.CoverageType,
            MarketTier = record.CatalogHealth,
            MonthlyListenership = GetMonthlyListenership(record),
            IsFlagshipStation = record.IsNational || string.Equals(record.CatalogHealth, "strong", StringComparison.OrdinalIgnoreCase),
            IsPremiumStation = string.Equals(record.CatalogHealth, "strong", StringComparison.OrdinalIgnoreCase),
            Metadata = seed.Metadata
        };
    }

    private static Dictionary<string, object?> ParseMetadata(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new Dictionary<string, object?>();
        }

        return JsonSerializer.Deserialize<Dictionary<string, object?>>(json)
               ?? new Dictionary<string, object?>();
    }

    private static Dictionary<string, object?> NormalizeMetadata(Dictionary<string, object?> metadata, OohPlanningInventoryRow row)
    {
        SetIfMissing(metadata, "sourceType", row.SourceType);
        SetIfMissing(metadata, "mediaType", row.MediaType);
        SetIfMissing(metadata, "displayName", row.DisplayName);
        SetIfMissing(metadata, "pricingModel", InferPricingModel(row));
        SetIfMissing(metadata, "rateBasis", InferRateBasis(row));
        SetIfMissing(metadata, "province", row.Province);
        SetIfMissing(metadata, "city", row.City);
        SetIfMissing(metadata, "suburb", row.Suburb);
        SetIfMissing(metadata, "area", row.Area);
        SetIfMissing(metadata, "language", row.Language);
        SetIfMissing(metadata, "timeBand", row.TimeBand);
        SetIfMissing(metadata, "time_band", row.TimeBand);
        SetIfMissing(metadata, "dayType", row.DayType);
        SetIfMissing(metadata, "day_type", row.DayType);
        SetIfMissing(metadata, "slotType", row.SlotType);
        SetIfMissing(metadata, "slot_type", row.SlotType);
        SetIfMissing(metadata, "durationSeconds", row.DurationSeconds);
        SetIfMissing(metadata, "duration_seconds", row.DurationSeconds);
        SetIfMissing(metadata, "regionClusterCode", row.RegionClusterCode);
        SetIfMissing(metadata, "region_cluster_code", row.RegionClusterCode);
        SetIfMissing(metadata, "marketScope", row.MarketScope);
        SetIfMissing(metadata, "market_scope", row.MarketScope);
        SetIfMissing(metadata, "marketTier", row.MarketTier);
        SetIfMissing(metadata, "market_tier", row.MarketTier);
        SetIfMissing(metadata, "latitude", row.Latitude);
        SetIfMissing(metadata, "longitude", row.Longitude);

        if (!metadata.ContainsKey("duration") && row.DurationSeconds.HasValue && row.DurationSeconds.Value > 0)
        {
            metadata["duration"] = $"{row.DurationSeconds.Value}s";
        }

        return metadata;
    }

    private static string InferPricingModel(OohPlanningInventoryRow row)
    {
        return row.SourceType switch
        {
            "ooh" => "fixed_placement_total",
            _ => row.PackageOnly ? "package_total" : "unit_rate"
        };
    }

    private static string InferRateBasis(OohPlanningInventoryRow row)
    {
        return row.SourceType switch
        {
            "ooh" => "per_placement",
            _ => row.PackageOnly ? "package" : "unit"
        };
    }

    private static void SetIfMissing(Dictionary<string, object?> metadata, string key, object? value)
    {
        if (value is null)
        {
            return;
        }

        if (value is string text && string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        if (!metadata.ContainsKey(key))
        {
            metadata[key] = value;
        }
    }

    private static string? GetMetadataValue(IReadOnlyDictionary<string, object?> metadata, string key)
    {
        if (!metadata.TryGetValue(key, out var value) || value is null)
        {
            return null;
        }

        return value switch
        {
            string text => string.IsNullOrWhiteSpace(text) ? null : text,
            JsonElement element => element.ValueKind switch
            {
                JsonValueKind.String => element.GetString(),
                JsonValueKind.Number => element.ToString(),
                JsonValueKind.True => bool.TrueString,
                JsonValueKind.False => bool.FalseString,
                _ => element.ToString()
            },
            _ => value.ToString()
        };
    }

    private static string? GetMetadataString(IReadOnlyDictionary<string, object?> metadata, string key) => GetMetadataValue(metadata, key);

    private static string? FirstNonEmpty(params string?[] values) => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

    private static int? ParseNullableInt(string? value) => int.TryParse(value, out var parsed) ? parsed : null;

    private static double? ParseNullableDouble(string? value)
    {
        return double.TryParse(value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    private static string NormalizeMediaType(string mediaType)
    {
        return mediaType.Trim().ToLowerInvariant() switch
        {
            "tv" => "TV",
            "radio" => "Radio",
            "ooh" => "OOH",
            _ => mediaType
        };
    }

    private static int? TryParseDuration(string? frequency)
    {
        if (string.IsNullOrWhiteSpace(frequency))
        {
            return null;
        }

        var text = frequency.Trim();
        return text.EndsWith("s", StringComparison.OrdinalIgnoreCase) && int.TryParse(text[..^1], out var seconds)
            ? seconds
            : null;
    }

    private static int? GetMonthlyListenership(BroadcastInventoryRecord record)
    {
        if (record.ListenershipDaily.HasValue)
        {
            return (int)Math.Min(int.MaxValue, record.ListenershipDaily.Value * 30L);
        }

        if (record.ListenershipWeekly.HasValue)
        {
            return (int)Math.Min(int.MaxValue, record.ListenershipWeekly.Value * 4L);
        }

        return null;
    }
}
