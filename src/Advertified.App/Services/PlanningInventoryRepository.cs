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
    null as Area,
    null as Language,
    null::int as LsmMin,
    null::int as LsmMax,
    coalesce((iif.metadata_json ->> 'discounted_rate_zar')::numeric, (iif.metadata_json ->> 'rate_card_zar')::numeric, 0) as Cost,
    coalesce(nullif(iif.metadata_json ->> 'available', ''), 'true') <> 'false' as IsAvailable,
    false as PackageOnly,
    null as TimeBand,
    null as DayType,
    null as SlotType,
    null::int as DurationSeconds,
    coalesce(rc.code, '') as RegionClusterCode,
    null as MarketScope,
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
    null as Province,
    null as City,
    null as Suburb,
    null as Area,
    rsf.metadata_json ->> 'language' as Language,
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
    null as Province,
    null as City,
    null as Suburb,
    null as Area,
    rpf.metadata_json ->> 'language' as Language,
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
        return new InventoryCandidate
        {
            SourceId = row.SourceId,
            SourceType = row.SourceType,
            DisplayName = row.DisplayName,
            MediaType = row.MediaType,
            Subtype = row.Subtype,
            Province = row.Province,
            City = row.City,
            Suburb = row.Suburb,
            Area = row.Area,
            Language = row.Language,
            LsmMin = row.LsmMin,
            LsmMax = row.LsmMax,
            Cost = row.Cost,
            IsAvailable = row.IsAvailable,
            PackageOnly = row.PackageOnly,
            TimeBand = row.TimeBand,
            DayType = row.DayType,
            SlotType = row.SlotType,
            DurationSeconds = row.DurationSeconds,
            RegionClusterCode = row.RegionClusterCode,
            MarketScope = row.MarketScope,
            MarketTier = row.MarketTier,
            MonthlyListenership = row.MonthlyListenership,
            IsFlagshipStation = row.IsFlagshipStation,
            IsPremiumStation = row.IsPremiumStation,
            Metadata = ParseMetadata(row.MetadataJson)
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
