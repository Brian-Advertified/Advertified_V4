using System.Data;
using System.Text.Json;
using Advertified.App.Contracts.Campaigns;
using Advertified.App.Domain.Campaigns;
using Advertified.App.Services.Abstractions;
using Dapper;
using Npgsql;

namespace Advertified.App.Services;

public sealed class PlanningInventoryRepository : IPlanningInventoryRepository
{
    private readonly string _connectionString;

    public PlanningInventoryRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<List<InventoryCandidate>> GetOohCandidatesAsync(CampaignPlanningRequest request, CancellationToken cancellationToken)
    {
        const string sql = @"
select
    iif.id as SourceId,
    'ooh' as SourceType,
    coalesce(iif.site_name, 'OOH Site') as DisplayName,
    'OOH' as MediaType,
    iif.media_type as Subtype,
    iif.province as Province,
    iif.city as City,
    iif.suburb as Suburb,
    coalesce(nullif(iif.suburb, ''), nullif(iif.city, ''), nullif(iif.province, '')) as Area,
    coalesce(nullif(iif.metadata_json ->> 'language', ''), 'N/A') as Language,
    null::int as LsmMin,
    null::int as LsmMax,
    coalesce((iif.metadata_json ->> 'discounted_rate_zar')::numeric, (iif.metadata_json ->> 'rate_card_zar')::numeric, 0) as Cost,
    coalesce(nullif(iif.metadata_json ->> 'available', ''), 'true') <> 'false' as IsAvailable,
    false as PackageOnly,
    coalesce(nullif(iif.metadata_json ->> 'time_band', ''), nullif(iif.metadata_json ->> 'daypart', ''), 'always_on') as TimeBand,
    null as DayType,
    coalesce(nullif(iif.media_type, ''), nullif(iif.metadata_json ->> 'slot_type', ''), 'placement') as SlotType,
    nullif(iif.metadata_json ->> 'duration_seconds', '')::int as DurationSeconds,
    coalesce(rc.code, '') as RegionClusterCode,
    coalesce(nullif(iif.metadata_json ->> 'geography_scope', ''), nullif(iif.province, ''), '') as MarketScope,
    null as MarketTier,
    null::int as MonthlyListenership,
    false as IsFlagshipStation,
    false as IsPremiumStation,
    iif.metadata_json::text as MetadataJson
from inventory_items_final iif
left join region_clusters rc on rc.id = iif.region_cluster_id
where coalesce((iif.metadata_json ->> 'discounted_rate_zar')::numeric, (iif.metadata_json ->> 'rate_card_zar')::numeric, 0) <= @Budget;";

        await using var conn = new NpgsqlConnection(_connectionString);
        var rows = await conn.QueryAsync<InventoryCandidateRow>(new CommandDefinition(
            sql,
            new { Budget = request.SelectedBudget },
            cancellationToken: cancellationToken));

        return rows.Select(Map).ToList();
    }

    public async Task<List<InventoryCandidate>> GetRadioSlotCandidatesAsync(CampaignPlanningRequest request, CancellationToken cancellationToken)
    {
        const string sql = @"
select
    rsf.id as SourceId,
    'radio_slot' as SourceType,
    concat(rs.name, ' - ', coalesce(nullif(rsf.time_band, ''), nullif(rsf.slot_type, ''), 'Slot')) as DisplayName,
    'Radio' as MediaType,
    rsf.slot_type as Subtype,
    nullif(rc.name, '') as Province,
    nullif(rsf.metadata_json ->> 'city', '') as City,
    null as Suburb,
    coalesce(nullif(rsf.metadata_json ->> 'show_name', ''), nullif(rsf.metadata_json ->> 'programme', '')) as Area,
    coalesce(nullif(rsf.metadata_json ->> 'language', ''), nullif(rsf.metadata_json ->> 'language_summary', '')) as Language,
    null::int as LsmMin,
    null::int as LsmMax,
    coalesce(rsf.rate, 0) as Cost,
    true as IsAvailable,
    false as PackageOnly,
    rsf.time_band as TimeBand,
    rsf.day_type as DayType,
    rsf.slot_type as SlotType,
    rsf.duration_seconds as DurationSeconds,
    coalesce(rc.code, '') as RegionClusterCode,
    coalesce(rs.market_scope, '') as MarketScope,
    coalesce(rs.market_tier, '') as MarketTier,
    rs.monthly_listenership as MonthlyListenership,
    coalesce(rs.is_flagship_station, false) as IsFlagshipStation,
    coalesce(rs.is_premium_station, false) as IsPremiumStation,
    rsf.metadata_json::text as MetadataJson
from radio_slots_final rsf
join radio_stations rs on rs.id = rsf.station_id
left join region_clusters rc on rc.id = rs.region_cluster_id
where coalesce(rsf.rate, 0) <= @Budget;";

        await using var conn = new NpgsqlConnection(_connectionString);
        var rows = await conn.QueryAsync<InventoryCandidateRow>(new CommandDefinition(
            sql,
            new { Budget = request.SelectedBudget },
            cancellationToken: cancellationToken));

        return rows.Select(Map).ToList();
    }

    public async Task<List<InventoryCandidate>> GetRadioPackageCandidatesAsync(CampaignPlanningRequest request, CancellationToken cancellationToken)
    {
        const string sql = @"
select
    rpf.id as SourceId,
    'radio_package' as SourceType,
    concat(rs.name, ' - ', rpf.name) as DisplayName,
    'Radio' as MediaType,
    'package' as Subtype,
    nullif(rc.name, '') as Province,
    nullif(rpf.metadata_json ->> 'city', '') as City,
    null as Suburb,
    coalesce(nullif(rpf.name, ''), nullif(rpf.metadata_json ->> 'package_name', '')) as Area,
    coalesce(nullif(rpf.metadata_json ->> 'language', ''), nullif(rpf.metadata_json ->> 'language_summary', '')) as Language,
    null::int as LsmMin,
    null::int as LsmMax,
    coalesce(rpf.total_cost, 0) as Cost,
    true as IsAvailable,
    true as PackageOnly,
    null as TimeBand,
    null as DayType,
    'package' as SlotType,
    null::int as DurationSeconds,
    coalesce(rc.code, '') as RegionClusterCode,
    coalesce(rs.market_scope, '') as MarketScope,
    coalesce(rs.market_tier, '') as MarketTier,
    rs.monthly_listenership as MonthlyListenership,
    coalesce(rs.is_flagship_station, false) as IsFlagshipStation,
    coalesce(rs.is_premium_station, false) as IsPremiumStation,
    rpf.metadata_json::text as MetadataJson
from radio_packages_final rpf
join radio_stations rs on rs.id = rpf.station_id
left join region_clusters rc on rc.id = rs.region_cluster_id
where coalesce(rpf.total_cost, 0) <= @Budget;";

        await using var conn = new NpgsqlConnection(_connectionString);
        var rows = await conn.QueryAsync<InventoryCandidateRow>(new CommandDefinition(
            sql,
            new { Budget = request.SelectedBudget },
            cancellationToken: cancellationToken));

        return rows.Select(Map).ToList();
    }

    private static InventoryCandidate Map(InventoryCandidateRow row)
    {
        var metadata = NormalizeMetadata(ParseMetadata(row.MetadataJson), row);

        return new InventoryCandidate
        {
            SourceId = row.SourceId,
            SourceType = row.SourceType,
            DisplayName = row.DisplayName,
            MediaType = row.MediaType,
            Subtype = row.Subtype,
            Province = FirstNonEmpty(row.Province, GetMetadataValue(metadata, "province")),
            City = FirstNonEmpty(row.City, GetMetadataValue(metadata, "city")),
            Suburb = FirstNonEmpty(row.Suburb, GetMetadataValue(metadata, "suburb")),
            Area = FirstNonEmpty(row.Area, GetMetadataValue(metadata, "area")),
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
            Metadata = metadata
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

    private static Dictionary<string, object?> NormalizeMetadata(Dictionary<string, object?> metadata, InventoryCandidateRow row)
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

        if (!metadata.ContainsKey("duration") && row.DurationSeconds.HasValue && row.DurationSeconds.Value > 0)
        {
            metadata["duration"] = $"{row.DurationSeconds.Value}s";
        }

        return metadata;
    }

    private static string InferPricingModel(InventoryCandidateRow row)
    {
        return row.SourceType switch
        {
            "radio_package" => "package_total",
            "radio_slot" => "per_spot_rate_card",
            "ooh" => "fixed_placement_total",
            _ => row.PackageOnly ? "package_total" : "unit_rate"
        };
    }

    private static string InferRateBasis(InventoryCandidateRow row)
    {
        return row.SourceType switch
        {
            "radio_package" => "package",
            "radio_slot" => "per_spot",
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

    private static string? FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
    }

    private static int? ParseNullableInt(string? value)
    {
        return int.TryParse(value, out var parsed) ? parsed : null;
    }

    private sealed class InventoryCandidateRow
    {
        public Guid SourceId { get; set; }
        public string SourceType { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string MediaType { get; set; } = string.Empty;
        public string? Subtype { get; set; }
        public string? Province { get; set; }
        public string? City { get; set; }
        public string? Suburb { get; set; }
        public string? Area { get; set; }
        public string? Language { get; set; }
        public int? LsmMin { get; set; }
        public int? LsmMax { get; set; }
        public decimal Cost { get; set; }
        public bool IsAvailable { get; set; }
        public bool PackageOnly { get; set; }
        public string? TimeBand { get; set; }
        public string? DayType { get; set; }
        public string? SlotType { get; set; }
        public int? DurationSeconds { get; set; }
        public string? RegionClusterCode { get; set; }
        public string? MarketScope { get; set; }
        public string? MarketTier { get; set; }
        public int? MonthlyListenership { get; set; }
        public bool IsFlagshipStation { get; set; }
        public bool IsPremiumStation { get; set; }
        public string? MetadataJson { get; set; }
    }
}
