using Advertified.App.Contracts.Packages;
using Advertified.App.Data;
using Advertified.App.Services.Abstractions;
using Dapper;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Text.Json;

namespace Advertified.App.Services;

public sealed class PackagePreviewService : IPackagePreviewService
{
    private readonly AppDbContext _db;
    private readonly string _connectionString;

    public PackagePreviewService(AppDbContext db, string connectionString)
    {
        _db = db;
        _connectionString = connectionString;
    }

    public async Task<PackagePreviewResult> GeneratePreviewAsync(Guid packageBandId, decimal budget, string? selectedArea, CancellationToken cancellationToken)
    {
        var band = await _db.PackageBands
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == packageBandId && x.IsActive, cancellationToken)
            ?? throw new InvalidOperationException("Package band not found.");
        var profile = await _db.PackageBandProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.PackageBandId == packageBandId, cancellationToken)
            ?? throw new InvalidOperationException("Package band profile not found.");

        if (budget < band.MinBudget || budget > band.MaxBudget)
        {
            throw new InvalidOperationException($"Selected budget must be between {band.MinBudget:0.##} and {band.MaxBudget:0.##}.");
        }

        var normalizedArea = NormalizeSelectedArea(selectedArea);

        await using var connection = new NpgsqlConnection(_connectionString);
        var budgetRatio = GetBudgetRatio(budget, band.MinBudget, band.MaxBudget);
        var tierCode = GetTierCode(budgetRatio);
        var tier = await _db.PackageBandPreviewTiers
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.PackageBandId == packageBandId && x.TierCode == tierCode, cancellationToken)
            ?? throw new InvalidOperationException("Package band preview tier not found.");

        var oohCandidates = (await connection.QueryAsync<OohPreviewRow>(
            new CommandDefinition(
                GetOohPreviewSql(),
                new
                {
                    PlacementBudget = budget * 0.35m,
                    PoolSize = 18
                },
                cancellationToken: cancellationToken)))
            .ToList();

        var oohExamples = SelectOohExamples(oohCandidates, normalizedArea, budget, budgetRatio);

        var radioCandidates = (await connection.QueryAsync<RadioPreviewRow>(
            new CommandDefinition(
                GetRadioPreviewSql(),
                new
                {
                    PoolSize = 80
                },
                cancellationToken: cancellationToken)))
            .ToList();

        var radioExamples = SelectRadioExamples(radioCandidates, normalizedArea, band.Code, budget, budgetRatio);
        var radioSupportExamples = radioExamples.Count > 0
            ? BuildRadioSupportExamples(radioExamples)
            : await BuildRadioShowFallbackExamplesAsync(connection, cancellationToken);
        radioSupportExamples = await BuildNationalFirstRadioPreviewExamplesAsync(connection, radioSupportExamples, normalizedArea, band.Code, cancellationToken);
        radioSupportExamples = await EnsureHighTierRadioPreviewExamplesAsync(connection, radioSupportExamples, radioCandidates, normalizedArea, band.Code, budget, budgetRatio, cancellationToken);
        var tvSupportExamples = new List<string>();
        var canShowTvExamples = profile.IncludeTv.Equals("yes", StringComparison.OrdinalIgnoreCase)
            || (profile.IncludeTv.Equals("optional", StringComparison.OrdinalIgnoreCase) && budgetRatio >= 0.55m);
        if (canShowTvExamples)
        {
            var tvCandidates = (await connection.QueryAsync<TvPreviewRow>(
                new CommandDefinition(
                    GetTvPreviewSql(),
                    new
                    {
                        PoolSize = 18,
                        MaxRate = budget * 0.22m,
                        MaxPackage = budget * 0.4m
                    },
                    cancellationToken: cancellationToken)))
                .ToList();

            tvSupportExamples = normalizedArea == "national"
                ? SelectTvExamples(tvCandidates, budget, budgetRatio)
                : BuildRegionalTvPreviewExamples(tvCandidates, budget, budgetRatio);
        }

        var reachEstimate = BuildReachEstimate(band.Code, budgetRatio, oohExamples.Count, radioExamples.Count);

        return new PackagePreviewResult
        {
            Budget = budget,
            SelectedArea = HumanizeSelectedArea(normalizedArea),
            TierLabel = tier.TierLabel,
            PackagePurpose = profile.PackagePurpose,
            RecommendedSpend = profile.RecommendedSpend,
            ReachEstimate = reachEstimate,
            Coverage = GetCoverageLabel(band.Code, budget, band.MinBudget, band.MaxBudget),
            ExampleLocations = BuildExampleLocations(oohExamples, normalizedArea),
            RadioSupportExamples = radioSupportExamples,
            TvSupportExamples = tvSupportExamples,
            TypicalInclusions = DeserializeList(tier.TypicalInclusionsJson),
            IndicativeMix = DeserializeList(tier.IndicativeMixJson),
            MediaMix = BuildMediaMix(band.Code, budget),
            Note = "Examples shown are indicative. Final media selection depends on your campaign brief, timing, and availability."
        };
    }

    private static string GetOohPreviewSql() =>
        @"
        select
            coalesce(nullif(iif.suburb, ''), nullif(iif.city, ''), 'Priority location') as Suburb,
            coalesce(nullif(iif.city, ''), nullif(iif.province, ''), 'South Africa') as City,
            coalesce(nullif(iif.province, ''), 'South Africa') as Province,
            coalesce(rc.code, '') as RegionClusterCode,
            coalesce(nullif(iif.site_name, ''), 'Premium placement') as SiteName,
            coalesce(
                case
                    when regexp_replace(coalesce(iif.metadata_json ->> 'discounted_rate_zar', ''), '[^0-9.]', '', 'g') = '' then null
                    else regexp_replace(coalesce(iif.metadata_json ->> 'discounted_rate_zar', ''), '[^0-9.]', '', 'g')::numeric
                end,
                case
                    when regexp_replace(coalesce(iif.metadata_json ->> 'rate_card_zar', ''), '[^0-9.]', '', 'g') = '' then null
                    else regexp_replace(coalesce(iif.metadata_json ->> 'rate_card_zar', ''), '[^0-9.]', '', 'g')::numeric
                end,
                0
            ) as Cost,
            case
                when regexp_replace(coalesce(iif.metadata_json ->> 'traffic_count', ''), '[^0-9]', '', 'g') = '' then 0
                else regexp_replace(coalesce(iif.metadata_json ->> 'traffic_count', ''), '[^0-9]', '', 'g')::bigint
            end as TrafficCount
        from inventory_items_final iif
        left join region_clusters rc on rc.id = iif.region_cluster_id
        where coalesce(
            case
                when regexp_replace(coalesce(iif.metadata_json ->> 'discounted_rate_zar', ''), '[^0-9.]', '', 'g') = '' then null
                else regexp_replace(coalesce(iif.metadata_json ->> 'discounted_rate_zar', ''), '[^0-9.]', '', 'g')::numeric
            end,
            case
                when regexp_replace(coalesce(iif.metadata_json ->> 'rate_card_zar', ''), '[^0-9.]', '', 'g') = '' then null
                else regexp_replace(coalesce(iif.metadata_json ->> 'rate_card_zar', ''), '[^0-9.]', '', 'g')::numeric
            end,
            0
        ) <= @PlacementBudget
        order by TrafficCount desc, Cost desc, iif.city nulls last, iif.suburb nulls last
        limit @PoolSize;
        ";

    private static string GetRadioShowPreviewSql() =>
        @"
        select
            rs.name as StationName,
            rsw.show_name as ShowName,
            coalesce(nullif(rsw.default_daypart, ''), 'selected dayparts') as Daypart
        from radio_shows rsw
        join radio_stations rs on rs.id = rsw.station_id
        where coalesce(nullif(rsw.show_name, ''), '') <> ''
        order by
            case coalesce(rsw.default_daypart, '')
                when 'breakfast' then 0
                when 'drive' then 1
                when 'midday' then 2
                else 3
            end,
            rs.name asc,
            rsw.show_name asc
        limit @PoolSize;
        ";

    private static string GetRadioPreviewSql() =>
        @"
        select
            rii.id as SourceId,
            rs.name as StationName,
            coalesce(nullif(rsw.show_name, ''), nullif(rii.inventory_name, ''), 'Radio support') as InventoryName,
            coalesce(nullif(rii.daypart, ''), nullif(rsw.default_daypart, ''), 'selected dayparts') as Daypart,
            coalesce(nullif(rii.inventory_kind, ''), 'slot') as InventoryKind,
            coalesce(rii.package_cost_zar, rii.rate_zar, 0) as Cost,
            coalesce(nullif(rii.geography_scope, ''), nullif(rsw.geography_scope, ''), '') as GeographyScope,
            coalesce(rc.code, '') as RegionClusterCode,
            coalesce(nullif(rs.market_scope, ''), '') as MarketScope,
            coalesce(nullif(rs.market_tier, ''), '') as MarketTier,
            coalesce(rs.monthly_listenership, 0) as MonthlyListenership,
            coalesce(rs.brand_strength_score, 0) as BrandStrengthScore,
            coalesce(rs.coverage_score, 0) as CoverageScore,
            coalesce(rs.audience_power_score, 0) as AudiencePowerScore,
            coalesce(nullif(rs.primary_audience, ''), '') as PrimaryAudience,
            coalesce(rs.is_flagship_station, false) as IsFlagshipStation,
            coalesce(rs.is_premium_station, false) as IsPremiumStation,
            mrs.show_or_programme_name as ReferenceShowName,
            coalesce(nullif(mrs.audience_summary, ''), nullif(rii.audience_summary, ''), nullif(rsw.audience_summary, ''), nullif(rs.audience_summary, ''), nullif(rs.primary_audience, ''), '') as AudienceSummary,
            coalesce(nullif(mrs.source_url, ''), nullif(rii.source_url, ''), nullif(rsw.source_url, ''), '') as SourceUrl
        from radio_inventory_items rii
        join radio_stations rs on rs.id = rii.station_id
        left join region_clusters rc on rc.id = coalesce(rii.region_cluster_id, rs.region_cluster_id)
        left join radio_shows rsw on rsw.id = rii.show_id
        left join lateral (
            select m.*
            from media_reference_sources m
            where m.media_channel = 'radio'
              and normalize_station_name(m.station_or_channel_name) = rs.normalized_name
            order by
                case
                    when coalesce(rii.daypart, rsw.default_daypart, '') ilike '%breakfast%'
                         and coalesce(m.metadata_json ->> 'daypart', '') = 'breakfast' then 0
                    when coalesce(rii.daypart, rsw.default_daypart, '') ilike '%drive%'
                         and coalesce(m.metadata_json ->> 'daypart', '') = 'drive' then 0
                    when (
                            lower(coalesce(rii.inventory_name, '')) like '%powerweek%'
                            or lower(coalesce(rii.inventory_name, '')) like '%workzone%'
                            or lower(coalesce(rii.inventory_name, '')) like '%lunch%'
                            or lower(coalesce(rii.inventory_name, '')) like '%biz%'
                         )
                         and lower(coalesce(m.show_or_programme_name, '')) like '%biz%' then 0
                    when (
                            lower(coalesce(rii.inventory_name, '')) like '%weekend%'
                            or lower(coalesce(rii.inventory_name, '')) like '%retail%'
                         )
                         and coalesce(m.metadata_json ->> 'daypart', '') = 'midday' then 1
                    when coalesce(rii.daypart, rsw.default_daypart, '') ilike '%midday%'
                         and coalesce(m.metadata_json ->> 'daypart', '') = 'midday' then 2
                    else 5
                end,
                m.updated_at desc
            limit 1
        ) mrs on true
        where rii.is_available = true
        order by
            case
                when coalesce(rii.package_cost_zar, rii.rate_zar, 0) > 0 then 0
                else 1
            end,
            case coalesce(rii.inventory_kind, '')
                when 'package' then 0
                when 'slot' then 1
                when 'rate_card' then 2
                else 3
            end,
            coalesce(rii.package_cost_zar, rii.rate_zar, 0) asc,
            rs.name asc
        limit @PoolSize;
        ";

    private static string GetTvPreviewSql() =>
        @"
        select
            tvi.id as SourceId,
            tc.channel_name as ChannelName,
            coalesce(nullif(tp.programme_name, ''), nullif(tvi.inventory_name, ''), 'TV support') as ProgrammeName,
            coalesce(nullif(tp.daypart, ''), nullif(tvi.inventory_kind, ''), 'selected slots') as Daypart,
            coalesce(nullif(tp.genre, ''), 'general') as Genre,
            coalesce(tvi.package_cost_zar, tvi.rate_zar, 0) as Cost,
            coalesce(nullif(tvi.audience_summary, ''), nullif(tp.audience_summary, ''), '') as AudienceSummary
        from tv_inventory_items tvi
        join tv_channels tc on tc.id = tvi.channel_id
        left join tv_programmes tp on tp.id = tvi.programme_id
        where tvi.is_available = true
          and (
              coalesce(tvi.rate_zar, 0) <= @MaxRate
              or coalesce(tvi.package_cost_zar, 0) <= @MaxPackage
              or (tvi.rate_zar is null and tvi.package_cost_zar is null)
          )
        order by
            case when lower(coalesce(tvi.inventory_kind, '')) = 'package' then 0 else 1 end,
            coalesce(tvi.package_cost_zar, tvi.rate_zar, 0) asc,
            tc.channel_name asc
        limit @PoolSize;
        ";

    private static string GetHighTierRadioStationPreviewSql() =>
        @"
        select
            rs.id as SourceId,
            rs.name as StationName,
            coalesce(nullif(mrs.show_or_programme_name, ''), concat(rs.name, ' - rate_book')) as InventoryName,
            coalesce(nullif(mrs.metadata_json ->> 'daypart', ''), 'selected dayparts') as Daypart,
            'rate_card' as InventoryKind,
            0::numeric as Cost,
            '' as GeographyScope,
            coalesce(rc.code, '') as RegionClusterCode,
            coalesce(nullif(rs.market_scope, ''), '') as MarketScope,
            coalesce(nullif(rs.market_tier, ''), '') as MarketTier,
            coalesce(rs.monthly_listenership, 0) as MonthlyListenership,
            coalesce(rs.brand_strength_score, 0) as BrandStrengthScore,
            coalesce(rs.coverage_score, 0) as CoverageScore,
            coalesce(rs.audience_power_score, 0) as AudiencePowerScore,
            coalesce(nullif(rs.primary_audience, ''), '') as PrimaryAudience,
            coalesce(rs.is_flagship_station, false) as IsFlagshipStation,
            coalesce(rs.is_premium_station, false) as IsPremiumStation,
            mrs.show_or_programme_name as ReferenceShowName,
            coalesce(nullif(mrs.audience_summary, ''), nullif(rs.audience_summary, ''), nullif(rs.primary_audience, ''), '') as AudienceSummary,
            coalesce(nullif(mrs.source_url, ''), nullif(rs.source_url, ''), '') as SourceUrl
        from radio_stations rs
        left join region_clusters rc on rc.id = rs.region_cluster_id
        left join lateral (
            select m.*
            from media_reference_sources m
            where m.media_channel = 'radio'
              and normalize_station_name(m.station_or_channel_name) = rs.normalized_name
            order by
                case coalesce(m.metadata_json ->> 'daypart', '')
                    when 'breakfast' then 0
                    when 'drive' then 1
                    when 'midday' then 2
                    else 3
                end,
                m.updated_at desc
            limit 1
        ) mrs on true
        where
            (coalesce(rs.is_flagship_station, false) = true or coalesce(rs.market_scope, '') = 'national')
            and (
                @SelectedArea = 'national'
                or coalesce(rc.code, '') = @SelectedArea
                or coalesce(rc.code, '') = 'national'
                or coalesce(rs.market_scope, '') = 'national'
            )
        order by
            coalesce(rs.monthly_listenership, 0) desc,
            coalesce(rs.brand_strength_score, 0) desc,
            coalesce(rs.audience_power_score, 0) desc
        limit 12;
        ";

    private static List<OohPreviewRow> SelectOohExamples(List<OohPreviewRow> candidates, string selectedArea, decimal budget, decimal budgetRatio)
    {
        if (candidates.Count == 0)
        {
            return new List<OohPreviewRow>();
        }

        var filteredCandidates = selectedArea == "national"
            ? candidates
            : candidates.Where(candidate => OohMatchesArea(candidate, selectedArea)).ToList();
        var targetPlacementCost = budget * (0.08m + (budgetRatio * 0.18m));

        return filteredCandidates
            .OrderByDescending(candidate => ScoreOohCandidate(candidate, targetPlacementCost, budgetRatio))
            .ThenByDescending(candidate => candidate.TrafficCount)
            .Take(12)
            .GroupBy(candidate => BuildAreaKey(candidate), StringComparer.OrdinalIgnoreCase)
            .Select(group => group
                .OrderByDescending(candidate => ScoreOohCandidate(candidate, targetPlacementCost, budgetRatio))
                .ThenByDescending(candidate => candidate.TrafficCount)
                .First())
            .Take(3)
            .ToList();
    }

    private static decimal ScoreOohCandidate(OohPreviewRow candidate, decimal targetPlacementCost, decimal budgetRatio)
    {
        var trafficScore = candidate.TrafficCount / 100000m;
        var costDistance = targetPlacementCost <= 0
            ? 1m
            : Math.Abs(candidate.Cost - targetPlacementCost) / targetPlacementCost;
        var affordabilityScore = Math.Max(0m, 10m - (costDistance * 10m));
        var premiumBias = budgetRatio >= 0.6m && candidate.Cost > targetPlacementCost ? 2m : 0m;

        return trafficScore + affordabilityScore + premiumBias;
    }

    private static string BuildAreaKey(OohPreviewRow row)
    {
        return $"{row.Suburb}|{row.City}".Trim().ToLowerInvariant();
    }

    private static bool OohMatchesArea(OohPreviewRow row, string selectedArea)
    {
        var clusterCode = row.RegionClusterCode?.Trim().ToLowerInvariant() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(clusterCode))
        {
            return selectedArea switch
            {
                "national" => true,
                _ => clusterCode == selectedArea
            };
        }

        var province = row.Province?.Trim().ToLowerInvariant() ?? string.Empty;
        var city = row.City?.Trim().ToLowerInvariant() ?? string.Empty;

        return selectedArea switch
        {
            "gauteng" => province.Contains("gauteng") || city.Contains("johannesburg") || city.Contains("pretoria"),
            "western-cape" => province.Contains("western cape") || city.Contains("cape town"),
            "eastern-cape" => province.Contains("eastern cape") || city.Contains("gqeberha") || city.Contains("port elizabeth"),
            _ => true
        };
    }

    private static bool RadioMatchesArea(RadioPreviewRow row, string selectedArea)
    {
        var clusterCode = row.RegionClusterCode?.Trim().ToLowerInvariant() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(clusterCode))
        {
            return selectedArea switch
            {
                "national" => true,
                _ => clusterCode == selectedArea || clusterCode == "national"
            };
        }

        var geography = row.GeographyScope?.Trim().ToLowerInvariant() ?? string.Empty;
        var station = row.StationName.Trim().ToLowerInvariant();

        return selectedArea switch
        {
            "gauteng" => geography.Contains("gauteng")
                || geography.Contains("metro")
                || geography.Contains("community")
                || station.Contains("kaya")
                || station.Contains("jozi")
                || station.Contains("metro fm"),
            "western-cape" => geography.Contains("western cape")
                || station.Contains("smile")
                || station.Contains("good hope"),
            "eastern-cape" => geography.Contains("eastern cape")
                || station.Contains("algoa"),
            _ => true
        };
    }

    private static List<string> BuildExampleLocations(List<OohPreviewRow> rows, string selectedArea)
    {
        if (rows.Count == 0)
        {
            return selectedArea switch
            {
                "gauteng" => new List<string>
                {
                    "Sandton, Johannesburg (premium commuter routes)",
                    "Sunnyside, Pretoria (high foot traffic)",
                    "Randburg, Johannesburg (urban visibility)"
                },
                "western-cape" => new List<string>
                {
                    "Cape Town CBD (strong commuter visibility)",
                    "Century City, Cape Town (retail and commuter traffic)",
                    "Canal Walk area (high shopper movement)"
                },
                "eastern-cape" => new List<string>
                {
                    "Gqeberha CBD (regional visibility)",
                    "Walmer, Gqeberha (commuter movement)",
                    "East London CBD (urban retail traffic)"
                },
                _ => new List<string>
                {
                    "Top commuter corridors",
                    "Retail-led urban nodes",
                    "High-traffic regional routes"
                }
            };
        }

        return rows
            .Select(row => new
            {
                Label = BuildExampleLocationLabel(row),
                row.TrafficCount
            })
            .GroupBy(x => x.Label, StringComparer.OrdinalIgnoreCase)
            .Select(group => group
                .OrderByDescending(x => x.TrafficCount)
                .First()
                .Label)
            .Take(3)
            .ToList();
    }

    private static string BuildExampleLocationLabel(OohPreviewRow row)
    {
        var areaLabel = row.Suburb.Equals(row.City, StringComparison.OrdinalIgnoreCase)
            ? row.Suburb
            : $"{row.Suburb}, {row.City}";

        var audienceCue = row.TrafficCount switch
        {
            >= 3000000 => "high-income commuter traffic",
            >= 1200000 => "strong retail and commuter movement",
            >= 600000 => "high foot and vehicle traffic",
            _ => "consistent local visibility"
        };

        return $"{areaLabel} ({audienceCue})";
    }

    private static List<RadioPreviewRow> SelectRadioExamples(List<RadioPreviewRow> candidates, string selectedArea, string bandCode, decimal budget, decimal budgetRatio)
    {
        if (candidates.Count == 0)
        {
            return new List<RadioPreviewRow>();
        }

        var normalizedBandCode = bandCode.Trim().ToLowerInvariant();
        var filteredCandidates = selectedArea == "national"
            ? candidates
            : candidates.Where(candidate => RadioMatchesArea(candidate, selectedArea)).ToList();
        if (filteredCandidates.Count == 0)
        {
            filteredCandidates = candidates;
        }

        if (normalizedBandCode is "scale" or "dominance")
        {
            var nationalCapableCandidates = candidates
                .Where(candidate => IsNationalRadioPreviewCandidate(candidate, normalizedBandCode))
                .ToList();
            if (nationalCapableCandidates.Count >= 2)
            {
                filteredCandidates = nationalCapableCandidates;
            }
        }

        var targetSupportCost = budget * (0.06m + (budgetRatio * 0.14m));
        var scoredCandidates = filteredCandidates
            .Select(candidate => new RadioPreviewCandidate(
                candidate,
                GetRadioPreviewScore(candidate, normalizedBandCode, targetSupportCost, budgetRatio)))
            .OrderByDescending(candidate => candidate.Score)
            .ThenByDescending(candidate => candidate.Row.Cost)
            .ToList();

        var rankedCandidates = scoredCandidates
            .Take(18)
            .ToList();

        return ApplyRadioPreviewDiversity(rankedCandidates, scoredCandidates, normalizedBandCode)
            .Take(3)
            .ToList();
    }

    private static decimal GetRadioPlanningScore(RadioPreviewRow candidate, string bandCode, decimal targetSupportCost, decimal budgetRatio)
    {
        var costDistance = targetSupportCost <= 0
            || candidate.Cost <= 0
            ? 1m
            : Math.Abs(candidate.Cost - targetSupportCost) / targetSupportCost;
        var affordabilityScore = candidate.Cost <= 0
            ? 4m
            : Math.Max(0m, 12m - (costDistance * 12m));
        var packageBias = candidate.InventoryKind.Equals("package", StringComparison.OrdinalIgnoreCase) ? 3m : 0m;
        var daypartBias = candidate.Daypart switch
        {
            var daypart when daypart.Contains("breakfast", StringComparison.OrdinalIgnoreCase) => 2m,
            var daypart when daypart.Contains("drive", StringComparison.OrdinalIgnoreCase) => 2m,
            _ => 0.5m
        };
        var referenceShowBias = !string.IsNullOrWhiteSpace(candidate.ReferenceShowName) ? 2.5m : 0m;
        var audienceBias = !string.IsNullOrWhiteSpace(candidate.AudienceSummary) ? 1.5m : 0m;
        var genericPenalty = candidate.StationName.Equals("Y", StringComparison.OrdinalIgnoreCase)
            && string.IsNullOrWhiteSpace(candidate.AudienceSummary)
            ? -4m
            : 0m;
        var marketScaleBias = GetRadioMarketScaleBias(candidate, bandCode, budgetRatio);
        var stationIntelligenceBias = GetRadioStationIntelligenceBias(candidate, bandCode);

        return affordabilityScore + packageBias + daypartBias + referenceShowBias + audienceBias + genericPenalty + marketScaleBias + stationIntelligenceBias;
    }

    private static decimal GetRadioPreviewScore(RadioPreviewRow candidate, string bandCode, decimal targetSupportCost, decimal budgetRatio)
    {
        return GetRadioPlanningScore(candidate, bandCode, targetSupportCost, budgetRatio)
            + GetRadioPreviewBandBonus(candidate, bandCode, targetSupportCost, budgetRatio);
    }

    private static decimal GetRadioPreviewBandBonus(RadioPreviewRow candidate, string bandCode, decimal targetSupportCost, decimal budgetRatio)
    {
        var station = candidate.StationName.Trim().ToLowerInvariant();
        var audience = DescribeRadioAudience(candidate.Daypart, candidate.InventoryName, candidate.AudienceSummary).ToLowerInvariant();
        var daypart = HumanizeDaypart(candidate.Daypart);
        var isPackage = candidate.InventoryKind.Equals("package", StringComparison.OrdinalIgnoreCase);
        var isFlagshipDaypart = daypart is "breakfast" or "drive-time";
        var isPremiumStation = candidate.IsPremiumStation || station.Contains("kaya") || station.Contains("metro") || station.Contains("5fm") || station.Contains("radio 2000");
        var isRegionalPremium = station.Contains("smile") || station.Contains("algoa");
        var isLocalStarter = station.Contains("jozi") || station.Contains("good hope");
        var isBusiness = audience.Contains("business");
        var isLifestyle = audience.Contains("lifestyle");
        var isCommuter = audience.Contains("commuter");
        var costRatio = targetSupportCost <= 0 || candidate.Cost <= 0
            ? 1m
            : candidate.Cost / targetSupportCost;

        return bandCode switch
        {
            "launch" =>
                (isLocalStarter ? 3m : 0m)
                + (isPackage ? 2m : 0m)
                + (isLifestyle || isCommuter ? 1.5m : 0m)
                - (isPremiumStation ? 2.5m : 0m)
                - (candidate.IsFlagshipStation ? 2m : 0m)
                - (isBusiness ? 1m : 0m)
                - (costRatio > 1.25m ? 2m : 0m),
            "boost" =>
                (isCommuter ? 2m : 0m)
                + (isLifestyle ? 2m : 0m)
                + (isRegionalPremium ? 1.5m : 0m)
                + (isFlagshipDaypart ? 1.5m : 0m)
                + (candidate.MonthlyListenership >= 1_500_000 ? 1.5m : 0m)
                - (costRatio > 1.45m ? 1m : 0m),
            "scale" =>
                (isPremiumStation ? 3m : 0m)
                + (isRegionalPremium ? 2m : 0m)
                + (isBusiness ? 2m : 0m)
                + (isFlagshipDaypart ? 2m : 0m)
                + (candidate.IsFlagshipStation ? 2.5m : 0m)
                + (candidate.MarketScope.Equals("national", StringComparison.OrdinalIgnoreCase) ? 2m : 0m)
                + (budgetRatio >= 0.65m && candidate.Cost > 0 ? 1m : 0m)
                - (isLocalStarter ? 1m : 0m),
            _ =>
                (isPremiumStation ? 4m : 0m)
                + (isBusiness ? 3m : 0m)
                + (isFlagshipDaypart ? 2.5m : 0m)
                + (isRegionalPremium ? 1.5m : 0m)
                + (candidate.IsFlagshipStation ? 3m : 0m)
                + (candidate.MarketScope.Equals("national", StringComparison.OrdinalIgnoreCase) ? 3m : 0m)
                + (candidate.MonthlyListenership >= 2_000_000 ? 2m : 0m)
                + (candidate.ReferenceShowName is not null ? 1.5m : 0m)
                - (isLocalStarter ? 2m : 0m)
        };
    }

    private static decimal GetRadioStationIntelligenceBias(RadioPreviewRow candidate, string bandCode)
    {
        var score = 0m;

        if (candidate.IsFlagshipStation)
        {
            score += 2.5m;
        }

        if (candidate.IsPremiumStation)
        {
            score += 1.5m;
        }

        if (candidate.MarketScope.Equals("national", StringComparison.OrdinalIgnoreCase))
        {
            score += bandCode switch
            {
                "launch" => -1.5m,
                "boost" => 0.5m,
                "scale" => 2.5m,
                _ => 3.5m
            };
        }

        if (candidate.MonthlyListenership >= 4_000_000)
        {
            score += bandCode switch
            {
                "launch" => -1m,
                "boost" => 1m,
                "scale" => 2m,
                _ => 3m
            };
        }
        else if (candidate.MonthlyListenership >= 1_500_000)
        {
            score += bandCode switch
            {
                "launch" => 0m,
                "boost" => 1m,
                "scale" => 1.5m,
                _ => 2m
            };
        }

        score += Math.Min(candidate.BrandStrengthScore / 2m, 3m);
        score += Math.Min(candidate.CoverageScore / 3m, 2m);
        score += Math.Min(candidate.AudiencePowerScore / 3m, 2m);

        return score;
    }

    private static decimal GetRadioMarketScaleBias(RadioPreviewRow candidate, string bandCode, decimal budgetRatio)
    {
        var geography = candidate.GeographyScope?.Trim().ToLowerInvariant() ?? string.Empty;
        var station = candidate.StationName.Trim().ToLowerInvariant();
        var audience = candidate.AudienceSummary?.Trim().ToLowerInvariant() ?? string.Empty;

        var isCommunity = geography.Contains("community") || audience.Contains("community");
        var isMetro = geography.Contains("metro");
        var isRegionalCommercial = geography.Contains("regional commercial");
        var isRegional = geography.Contains("regional");
        var isBusinessSkew = audience.Contains("business") || station.Contains("kaya");
        var isPremiumRegional = station.Contains("smile") || station.Contains("algoa");

        return bandCode switch
        {
            "launch" => (isCommunity ? 3m : 0m) + (isRegional ? 1.5m : 0m) - (isMetro ? 1m : 0m),
            "boost" => (isRegionalCommercial ? 2.5m : 0m) + (isMetro ? 1.5m : 0m) + (isCommunity ? 0.5m : 0m),
            "scale" => (isMetro ? 4m : 0m) + (isRegionalCommercial ? 3m : 0m) + (isBusinessSkew ? 1m : 0m) + (isPremiumRegional ? 1m : 0m) - (isCommunity ? 5m : 0m),
            _ => (isMetro ? 4.5m : 0m) + (isRegionalCommercial ? 3m : 0m) + (isBusinessSkew ? 2m : 0m) + (isPremiumRegional ? 1.5m : 0m) - (isCommunity ? 7m : 0m) + (budgetRatio >= 0.6m && candidate.Cost > 0 ? 1m : 0m)
        };
    }

    private static bool IsNationalRadioPreviewCandidate(RadioPreviewRow candidate, string bandCode)
    {
        var station = candidate.StationName.Trim().ToLowerInvariant();
        var marketScope = candidate.MarketScope?.Trim().ToLowerInvariant() ?? string.Empty;
        var clusterCode = candidate.RegionClusterCode?.Trim().ToLowerInvariant() ?? string.Empty;
        var geography = candidate.GeographyScope?.Trim().ToLowerInvariant() ?? string.Empty;
        var marketTier = candidate.MarketTier?.Trim().ToLowerInvariant() ?? string.Empty;

        var isNational = marketScope == "national" || clusterCode == "national";
        var isFlagship = candidate.IsFlagshipStation;
        var isPremium = candidate.IsPremiumStation || marketTier is "premium" or "flagship";
        var isProvincialOnly = station.Contains("kaya") || station.Contains("jozi") || station.Contains("smile") || station.Contains("algoa") || geography.Contains("community");

        if (bandCode == "dominance")
        {
            return isNational && (isFlagship || isPremium) && !isProvincialOnly;
        }

        return (isNational || isFlagship) && !isProvincialOnly;
    }

    private static List<RadioPreviewRow> ApplyRadioPreviewDiversity(List<RadioPreviewCandidate> rankedCandidates, List<RadioPreviewCandidate> scoredCandidates, string bandCode)
    {
        var selected = new List<RadioPreviewRow>();
        var reservedHighTierCandidate = GetReservedHighTierRadioCandidate(scoredCandidates, bandCode);

        if (reservedHighTierCandidate is not null)
        {
            selected.Add(reservedHighTierCandidate);
        }

        foreach (var candidate in rankedCandidates)
        {
            if (selected.Any(existing => existing.StationName.Equals(candidate.Row.StationName, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            var penalty = GetRadioPreviewDiversityPenalty(candidate.Row, selected);
            if (candidate.Score - penalty < 4m)
            {
                continue;
            }

            selected.Add(candidate.Row);
            if (selected.Count >= 3)
            {
                break;
            }
        }

        if (selected.Count == 0)
        {
            selected.AddRange(rankedCandidates.Take(3).Select(candidate => candidate.Row));
            return selected;
        }

        var fallbackCandidates = rankedCandidates
            .Select(candidate => candidate.Row)
            .Where(candidate => !selected.Contains(candidate))
            .ToList();

        foreach (var candidate in fallbackCandidates)
        {
            if (selected.Count >= 3)
            {
                break;
            }

            if (selected.Any(existing => existing.StationName.Equals(candidate.StationName, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            if (GetRadioPreviewDiversityPenalty(candidate, selected) >= 6m && selected.Count > 0)
            {
                continue;
            }

            selected.Add(candidate);
        }

        if ((bandCode == "scale" || bandCode == "dominance")
            && !selected.Any(candidate => candidate.IsFlagshipStation || candidate.MarketScope.Equals("national", StringComparison.OrdinalIgnoreCase)))
        {
            var flagshipCandidate = scoredCandidates
                .Select(candidate => candidate.Row)
                .FirstOrDefault(candidate =>
                    (candidate.IsFlagshipStation || candidate.MarketScope.Equals("national", StringComparison.OrdinalIgnoreCase))
                    && !selected.Any(existing => existing.SourceId == candidate.SourceId));

            if (flagshipCandidate is not null)
            {
                if (selected.Count >= 3)
                {
                    selected[selected.Count - 1] = flagshipCandidate;
                }
                else
                {
                    selected.Add(flagshipCandidate);
                }
            }
        }

        if (selected.Count < 2)
        {
            var bestByStation = scoredCandidates
                .Where(candidate => !selected.Any(existing => existing.SourceId == candidate.Row.SourceId))
                .GroupBy(candidate => candidate.Row.StationName, StringComparer.OrdinalIgnoreCase)
                .Select(group => group
                    .OrderByDescending(candidate => candidate.Score)
                    .ThenByDescending(candidate => candidate.Row.Cost)
                    .Select(candidate => candidate.Row)
                    .First())
                .ToList();

            foreach (var candidate in bestByStation)
            {
                if (selected.Count >= 3)
                {
                    break;
                }

                selected.Add(candidate);
            }
        }

        return selected;
    }

    private static RadioPreviewRow? GetReservedHighTierRadioCandidate(List<RadioPreviewCandidate> scoredCandidates, string bandCode)
    {
        if (bandCode != "scale" && bandCode != "dominance")
        {
            return null;
        }

        return scoredCandidates
            .Where(candidate => IsReservedHighTierRadioCandidate(candidate.Row, bandCode))
            .OrderByDescending(candidate => candidate.Score)
            .ThenByDescending(candidate => candidate.Row.MonthlyListenership)
            .ThenByDescending(candidate => candidate.Row.BrandStrengthScore)
            .Select(candidate => candidate.Row)
            .FirstOrDefault();
    }

    private static bool IsReservedHighTierRadioCandidate(RadioPreviewRow candidate, string bandCode)
    {
        var station = candidate.StationName.Trim().ToLowerInvariant();
        var isNational = candidate.MarketScope.Equals("national", StringComparison.OrdinalIgnoreCase);
        var isFlagship = candidate.IsFlagshipStation;
        var isPremium = candidate.IsPremiumStation || candidate.MarketTier.Equals("flagship", StringComparison.OrdinalIgnoreCase) || candidate.MarketTier.Equals("premium", StringComparison.OrdinalIgnoreCase);
        var hasReachSignal = candidate.MonthlyListenership >= 800_000 || candidate.BrandStrengthScore >= 8m || candidate.AudiencePowerScore >= 7m;
        var supportedBrand = station.Contains("metro") || station.Contains("5fm") || station.Contains("radio 2000") || station.Contains("safm");

        if (bandCode == "dominance")
        {
            return isNational && (isFlagship || isPremium) && (hasReachSignal || supportedBrand);
        }

        return (isNational || isFlagship) && (isPremium || hasReachSignal || supportedBrand);
    }

    private static decimal GetRadioPreviewDiversityPenalty(RadioPreviewRow candidate, List<RadioPreviewRow> selected)
    {
        decimal penalty = 0m;
        var candidateAudience = DescribeRadioAudience(candidate.Daypart, candidate.InventoryName, candidate.AudienceSummary);
        var candidateDaypart = HumanizeDaypart(candidate.Daypart);

        foreach (var existing in selected)
        {
            if (existing.StationName.Equals(candidate.StationName, StringComparison.OrdinalIgnoreCase))
            {
                penalty += 4m;
            }

            if (string.Equals(HumanizeDaypart(existing.Daypart), candidateDaypart, StringComparison.OrdinalIgnoreCase))
            {
                penalty += 2m;
            }

            var existingAudience = DescribeRadioAudience(existing.Daypart, existing.InventoryName, existing.AudienceSummary);
            if (string.Equals(existingAudience, candidateAudience, StringComparison.OrdinalIgnoreCase))
            {
                penalty += 1.5m;
            }
        }

        return penalty;
    }

    private static async Task<List<string>> EnsureHighTierRadioPreviewExamplesAsync(
        NpgsqlConnection connection,
        List<string> radioSupportExamples,
        List<RadioPreviewRow> radioCandidates,
        string selectedArea,
        string bandCode,
        decimal budget,
        decimal budgetRatio,
        CancellationToken cancellationToken)
    {
        var normalizedBandCode = bandCode.Trim().ToLowerInvariant();
        if (normalizedBandCode != "scale" && normalizedBandCode != "dominance")
        {
            return radioSupportExamples;
        }

        if (radioCandidates.Count == 0)
        {
            return radioSupportExamples;
        }

        var targetSupportCost = budget * (0.06m + (budgetRatio * 0.14m));
        var filteredCandidates = selectedArea == "national"
            ? radioCandidates
            : radioCandidates.Where(candidate => RadioMatchesArea(candidate, selectedArea)).ToList();
        if (filteredCandidates.Count == 0)
        {
            filteredCandidates = radioCandidates;
        }

        var reservedCandidate = filteredCandidates
            .Select(candidate => new RadioPreviewCandidate(
                candidate,
                GetRadioPreviewScore(candidate, normalizedBandCode, targetSupportCost, budgetRatio)))
            .Where(candidate => IsReservedHighTierRadioCandidate(candidate.Row, normalizedBandCode))
            .OrderByDescending(candidate => candidate.Score)
            .ThenByDescending(candidate => candidate.Row.MonthlyListenership)
            .ThenByDescending(candidate => candidate.Row.BrandStrengthScore)
            .Select(candidate => candidate.Row)
            .FirstOrDefault();

        if (reservedCandidate is null)
        {
            reservedCandidate = await GetReservedHighTierRadioStationCandidateAsync(connection, selectedArea, normalizedBandCode, cancellationToken);
            if (reservedCandidate is null)
            {
                return radioSupportExamples;
            }
        }

        var reservedLabel = BuildRadioSupportLabel(reservedCandidate);
        if (radioSupportExamples.Any(example => example.Equals(reservedLabel, StringComparison.OrdinalIgnoreCase)
            || example.StartsWith($"{reservedCandidate.StationName.Trim()} - ", StringComparison.OrdinalIgnoreCase)))
        {
            return radioSupportExamples;
        }

        var updated = new List<string> { reservedLabel };
        updated.AddRange(radioSupportExamples);
        return updated
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(3)
            .ToList();
    }

    private static async Task<List<string>> BuildNationalFirstRadioPreviewExamplesAsync(
        NpgsqlConnection connection,
        List<string> radioSupportExamples,
        string selectedArea,
        string bandCode,
        CancellationToken cancellationToken)
    {
        var normalizedBandCode = bandCode.Trim().ToLowerInvariant();
        if (normalizedBandCode is not "scale" and not "dominance")
        {
            return radioSupportExamples;
        }

        var rows = (await connection.QueryAsync<RadioPreviewRow>(
            new CommandDefinition(
                GetHighTierRadioStationPreviewSql(),
                new { SelectedArea = selectedArea },
                cancellationToken: cancellationToken)))
            .ToList();

        var examples = rows
            .Where(row => IsNationalRadioPreviewCandidate(row, normalizedBandCode))
            .OrderByDescending(row => row.MonthlyListenership)
            .ThenByDescending(row => row.BrandStrengthScore)
            .ThenByDescending(row => row.AudiencePowerScore)
            .Select(BuildRadioSupportLabel)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(3)
            .ToList();

        return examples.Count >= 2 ? examples : radioSupportExamples;
    }

    private static async Task<RadioPreviewRow?> GetReservedHighTierRadioStationCandidateAsync(
        NpgsqlConnection connection,
        string selectedArea,
        string bandCode,
        CancellationToken cancellationToken)
    {
        var rows = (await connection.QueryAsync<RadioPreviewRow>(
            new CommandDefinition(
                GetHighTierRadioStationPreviewSql(),
                new
                {
                    SelectedArea = selectedArea
                },
                cancellationToken: cancellationToken)))
            .ToList();

        return rows
            .Where(candidate => IsReservedHighTierRadioCandidate(candidate, bandCode))
            .OrderByDescending(candidate => candidate.MonthlyListenership)
            .ThenByDescending(candidate => candidate.BrandStrengthScore)
            .ThenByDescending(candidate => candidate.AudiencePowerScore)
            .FirstOrDefault();
    }

    private static List<string> SelectTvExamples(List<TvPreviewRow> candidates, decimal budget, decimal budgetRatio)
    {
        if (candidates.Count == 0)
        {
            return new List<string>();
        }

        var targetSupportCost = budget * (0.08m + (budgetRatio * 0.18m));

        return candidates
            .OrderByDescending(candidate => ScoreTvCandidate(candidate, targetSupportCost))
            .ThenBy(candidate => candidate.Cost)
            .Take(12)
            .GroupBy(candidate => $"{candidate.ChannelName}|{candidate.ProgrammeName}", StringComparer.OrdinalIgnoreCase)
            .Select(group => group
                .OrderByDescending(candidate => ScoreTvCandidate(candidate, targetSupportCost))
                .ThenBy(candidate => candidate.Cost)
                .First())
            .Take(3)
            .Select(BuildTvSupportLabel)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static List<string> BuildRegionalTvPreviewExamples(List<TvPreviewRow> candidates, decimal budget, decimal budgetRatio)
    {
        var examples = SelectTvExamples(candidates, budget, budgetRatio)
            .Take(2)
            .Select(example => $"{example} - available for broader or national campaigns")
            .ToList();

        if (examples.Count == 0)
        {
            return new List<string>();
        }

        examples.Insert(0, "TV can be included for broader or national campaigns at this package level");
        return examples;
    }

    private static decimal ScoreTvCandidate(TvPreviewRow candidate, decimal targetSupportCost)
    {
        var costDistance = targetSupportCost <= 0 || candidate.Cost <= 0
            ? 1m
            : Math.Abs(candidate.Cost - targetSupportCost) / targetSupportCost;
        var affordabilityScore = candidate.Cost <= 0
            ? 4m
            : Math.Max(0m, 12m - (costDistance * 12m));
        var genreBias = candidate.Genre switch
        {
            var genre when genre.Contains("sport", StringComparison.OrdinalIgnoreCase) => 2.5m,
            var genre when genre.Contains("news", StringComparison.OrdinalIgnoreCase) => 2m,
            var genre when genre.Contains("lifestyle", StringComparison.OrdinalIgnoreCase) => 2m,
            _ => 1m
        };
        var daypartBias = candidate.Daypart switch
        {
            var daypart when daypart.Contains("breakfast", StringComparison.OrdinalIgnoreCase) => 1.5m,
            var daypart when daypart.Contains("drive", StringComparison.OrdinalIgnoreCase) => 2m,
            _ => 0.5m
        };
        var audienceBias = !string.IsNullOrWhiteSpace(candidate.AudienceSummary) ? 1.5m : 0m;

        return affordabilityScore + genreBias + daypartBias + audienceBias;
    }

    private static string BuildTvSupportLabel(TvPreviewRow row)
    {
        var audience = DescribeTvAudience(row.AudienceSummary, row.Genre, row.Daypart);
        var programmeLabel = HumanizeTvProgramme(row.ProgrammeName, row.Genre, row.Daypart);
        return $"{row.ChannelName.Trim()} - {programmeLabel} ({audience})";
    }

    private static string BuildRadioKey(RadioPreviewRow row)
    {
        return $"{row.StationName}|{row.Daypart}|{row.InventoryKind}".Trim().ToLowerInvariant();
    }

    private static List<string> BuildRadioSupportExamples(List<RadioPreviewRow> rows)
    {
        return rows
            .Select(BuildRadioSupportLabel)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(3)
            .ToList();
    }

    private static async Task<List<string>> BuildRadioShowFallbackExamplesAsync(NpgsqlConnection connection, CancellationToken cancellationToken)
    {
        var rows = (await connection.QueryAsync<RadioShowPreviewRow>(
            new CommandDefinition(
                GetRadioShowPreviewSql(),
                new { PoolSize = 12 },
                cancellationToken: cancellationToken)))
            .ToList();

        var showExamples = rows
            .GroupBy(row => $"{row.StationName}|{row.Daypart}", StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .Take(3)
            .Select(BuildRadioShowSupportLabel)
            .ToList();

        if (showExamples.Count > 0)
        {
            return showExamples;
        }

        var documentRows = (await connection.QueryAsync<RadioDocumentPreviewRow>(
            new CommandDefinition(
                GetRadioDocumentPreviewSql(),
                new { PoolSize = 6 },
                cancellationToken: cancellationToken)))
            .ToList();

        return documentRows
            .Select(BuildRadioDocumentSupportLabel)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(3)
            .ToList();
    }

    private static string BuildRadioSupportLabel(RadioPreviewRow row)
    {
        var station = row.StationName.Trim();
        var daypart = HumanizeDaypart(row.Daypart);
        var audience = DescribeRadioAudience(row.Daypart, row.InventoryName, row.AudienceSummary);
        var referenceShow = row.ReferenceShowName?.Trim();
        var inventoryName = row.InventoryName.Trim();

        if (inventoryName.Contains("rate_book", StringComparison.OrdinalIgnoreCase)
            || inventoryName.Contains("rate book", StringComparison.OrdinalIgnoreCase))
        {
            var descriptor = row.MarketScope.Equals("national", StringComparison.OrdinalIgnoreCase)
                ? "national reach option"
                : "broader reach option";
            return $"{station} - {descriptor} ({audience})";
        }

        if (!string.IsNullOrWhiteSpace(referenceShow))
        {
            return $"{station} - {referenceShow} ({audience})";
        }

        if (IsMeaningfulPackageName(inventoryName, station))
        {
            return $"{station} - {inventoryName} ({audience})";
        }

        if (row.InventoryKind.Equals("package", StringComparison.OrdinalIgnoreCase))
        {
            return $"{station} - {daypart} package ({audience})";
        }

        if (row.InventoryKind.Equals("rate_card", StringComparison.OrdinalIgnoreCase))
        {
            return $"{station} - {daypart} spots ({audience})";
        }

        return $"{station} - {daypart} slot support ({audience})";
    }

    private static string BuildRadioShowSupportLabel(RadioShowPreviewRow row)
    {
        var station = row.StationName.Trim();
        var showName = row.ShowName.Trim();
        var daypart = HumanizeDaypart(row.Daypart);
        var audience = DescribeRadioAudience(row.Daypart, row.ShowName, null);

        if (showName.Contains(station, StringComparison.OrdinalIgnoreCase))
        {
            return $"{showName} - {daypart} ({audience})";
        }

        return $"{station} - {showName} ({audience})";
    }

    private static string GetRadioDocumentPreviewSql() =>
        @"
        select
            coalesce(nullif(sd.supplier_name, ''), nullif(sd.document_title, ''), 'Radio partner') as SourceName,
            coalesce(nullif(sd.document_title, ''), 'Radio package') as DocumentTitle
        from source_documents sd
        where lower(sd.media_channel) = 'radio'
        order by
            coalesce(nullif(sd.supplier_name, ''), nullif(sd.document_title, ''), 'Radio partner') asc,
            sd.document_title asc
        limit @PoolSize;
        ";

    private static string BuildRadioDocumentSupportLabel(RadioDocumentPreviewRow row)
    {
        var sourceName = row.SourceName.Trim();
        var title = row.DocumentTitle.Trim();

        if (title.Contains("sport", StringComparison.OrdinalIgnoreCase))
        {
            return $"{sourceName} - sports programming (sport audience)";
        }

        if (title.Contains("package", StringComparison.OrdinalIgnoreCase))
        {
            return $"{sourceName} - selected package support ({DescribeRadioAudience(null, title)})";
        }

        return $"{sourceName} - selected market support ({DescribeRadioAudience(null, title)})";
    }

    private static string HumanizeDaypart(string? daypart)
    {
        if (string.IsNullOrWhiteSpace(daypart))
        {
            return "selected market";
        }

        return daypart.Trim().ToLowerInvariant() switch
        {
            "breakfast" => "breakfast",
            "drive" => "drive-time",
            "midday" => "midday",
            _ => daypart.Trim().ToLowerInvariant()
        };
    }

    private static string DescribeRadioAudience(string? daypart, string? context, string? explicitAudienceSummary = null)
    {
        if (!string.IsNullOrWhiteSpace(explicitAudienceSummary))
        {
            return ToAudienceLabel(explicitAudienceSummary!);
        }

        var normalizedContext = context?.Trim().ToLowerInvariant() ?? string.Empty;
        if (normalizedContext.Contains("sport"))
        {
            return "sport audience";
        }

        if (normalizedContext.Contains("business"))
        {
            return "business audience";
        }

        if (normalizedContext.Contains("lifestyle"))
        {
            return "lifestyle audience";
        }

        var normalizedDaypart = daypart?.Trim().ToLowerInvariant() ?? string.Empty;
        return normalizedDaypart switch
        {
            "breakfast" => "high commuter audience",
            "drive" => "urban commuter audience",
            "drive-time" => "urban commuter audience",
            "midday" => "regional lifestyle audience",
            _ => "selected audience fit"
        };
    }

    private static string ToAudienceLabel(string summary)
    {
        var normalized = summary.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return "selected audience fit";
        }

        var lowered = normalized.ToLowerInvariant();
        if (lowered.Contains("commuter"))
        {
            return "commuter audience";
        }

        if (lowered.Contains("business"))
        {
            return "business audience";
        }

        if (lowered.Contains("lifestyle"))
        {
            return "lifestyle audience";
        }

        if (lowered.Contains("community"))
        {
            return "community audience";
        }

        if (lowered.Contains("adult contemporary"))
        {
            return "adult contemporary audience";
        }

        if (lowered.Contains("urban"))
        {
            return "urban audience";
        }

        return "selected audience fit";
    }

    private static string DescribeTvAudience(string? explicitAudienceSummary, string? genre, string? daypart)
    {
        if (!string.IsNullOrWhiteSpace(explicitAudienceSummary))
        {
            var lowered = explicitAudienceSummary.Trim().ToLowerInvariant();
            if (lowered.Contains("sport"))
            {
                return "sport audience";
            }

            if (lowered.Contains("news"))
            {
                return "adult news audience";
            }

            if (lowered.Contains("youth"))
            {
                return "youth audience";
            }

            if (lowered.Contains("family"))
            {
                return "family audience";
            }

            if (lowered.Contains("lifestyle"))
            {
                return "lifestyle audience";
            }
        }

        var normalizedGenre = genre?.Trim().ToLowerInvariant() ?? string.Empty;
        if (normalizedGenre.Contains("sport"))
        {
            return "sport audience";
        }

        if (normalizedGenre.Contains("news"))
        {
            return "adult news audience";
        }

        if (normalizedGenre.Contains("youth"))
        {
            return "youth audience";
        }

        if (normalizedGenre.Contains("lifestyle"))
        {
            return "lifestyle audience";
        }

        return HumanizeDaypart(daypart) switch
        {
            "breakfast" => "morning household audience",
            "drive-time" => "prime-time audience",
            "midday" => "daytime audience",
            _ => "broad television audience"
        };
    }

    private static string HumanizeTvProgramme(string programmeName, string? genre, string? daypart)
    {
        var normalized = programmeName.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return "selected programming";
        }

        if (normalized.Contains("package", StringComparison.OrdinalIgnoreCase))
        {
            if (!string.IsNullOrWhiteSpace(genre) && genre.Contains("sport", StringComparison.OrdinalIgnoreCase))
            {
                return "sports package";
            }

            if (!string.IsNullOrWhiteSpace(genre) && genre.Contains("news", StringComparison.OrdinalIgnoreCase))
            {
                return "news package";
            }

            return $"{HumanizeDaypart(daypart)} package";
        }

        if (normalized.Contains("Expresso", StringComparison.OrdinalIgnoreCase))
        {
            return "Expresso morning slots";
        }

        if (normalized.Contains("YO TV", StringComparison.OrdinalIgnoreCase))
        {
            return "YO TV youth slots";
        }

        return normalized;
    }

    private static bool IsMeaningfulPackageName(string inventoryName, string station)
    {
        if (string.IsNullOrWhiteSpace(inventoryName))
        {
            return false;
        }

        var normalized = inventoryName.Trim();
        if (normalized.Equals(station, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var genericMarkers = new[]
        {
            "selected dayparts",
            "radio support",
            "commercial",
            "radio slot"
        };

        return !genericMarkers.Any(marker => normalized.Equals(marker, StringComparison.OrdinalIgnoreCase));
    }

    private static string BuildReachEstimate(string bandCode, decimal budgetRatio, int oohCount, int radioCount)
    {
        var (minLow, maxLow, minHigh, maxHigh) = GetReachEnvelope(bandCode);
        var normalizedRatio = Math.Clamp(budgetRatio, 0m, 1m);
        var low = Interpolate(minLow, maxLow, normalizedRatio);
        var high = Interpolate(minHigh, maxHigh, normalizedRatio);
        var supportLift = 1m + (Math.Min(3, oohCount) * 0.03m) + (Math.Min(3, radioCount) * 0.035m);

        low = RoundReach(low * supportLift);
        high = RoundReach(high * supportLift);

        if (high <= low)
        {
            high = RoundReach(low * 1.35m);
        }

        return $"~{FormatReachValue(low)} - {FormatReachValue(high)} impressions";
    }

    private static (decimal MinLow, decimal MaxLow, decimal MinHigh, decimal MaxHigh) GetReachEnvelope(string bandCode)
    {
        return bandCode.Trim().ToLowerInvariant() switch
        {
            "launch" => (60000m, 180000m, 180000m, 420000m),
            "boost" => (180000m, 550000m, 450000m, 1200000m),
            "scale" => (550000m, 1200000m, 1400000m, 3200000m),
            _ => (1200000m, 2600000m, 3000000m, 6500000m)
        };
    }

    private static decimal Interpolate(decimal min, decimal max, decimal ratio)
    {
        return min + ((max - min) * ratio);
    }

    private static decimal RoundReach(decimal reach)
    {
        if (reach < 250000m)
        {
            return RoundToIncrement(reach, 10000m);
        }

        if (reach < 1000000m)
        {
            return RoundToIncrement(reach, 25000m);
        }

        return RoundToIncrement(reach, 100000m);
    }

    private static decimal RoundToIncrement(decimal value, decimal increment)
    {
        if (increment <= 0)
        {
            return Math.Round(value, MidpointRounding.AwayFromZero);
        }

        return Math.Round(value / increment, MidpointRounding.AwayFromZero) * increment;
    }

    private static string FormatReachValue(decimal value)
    {
        if (value >= 1000000m)
        {
            var millions = value / 1000000m;
            return $"{millions:0.#}M";
        }

        if (value >= 1000m)
        {
            var thousands = value / 1000m;
            return $"{thousands:0.#}K";
        }

        return value.ToString("0");
    }

    private static decimal GetBudgetRatio(decimal budget, decimal minBudget, decimal maxBudget)
    {
        var span = maxBudget - minBudget;
        if (span <= 0)
        {
            return 0m;
        }

        return Math.Clamp((budget - minBudget) / span, 0m, 1m);
    }

    private static string GetTierCode(decimal budgetRatio)
    {
        if (budgetRatio < 0.34m)
        {
            return "entry";
        }

        if (budgetRatio < 0.72m)
        {
            return "mid";
        }

        return "premium";
    }

    private static string GetCoverageLabel(string bandCode, decimal budget, decimal minBudget, decimal maxBudget)
    {
        var ratio = maxBudget <= minBudget ? 0m : (budget - minBudget) / (maxBudget - minBudget);
        var normalizedCode = bandCode.Trim().ToLowerInvariant();

        return normalizedCode switch
        {
            "launch" => ratio < 0.65m ? "Single area -> focused local coverage" : "Local -> multi-area starter coverage",
            "boost" => ratio < 0.65m ? "Local -> regional coverage" : "Regional -> selected multi-area coverage",
            "scale" => ratio < 0.65m ? "Regional -> broader market coverage" : "Broad regional -> multi-zone coverage",
            _ => ratio < 0.65m ? "Regional -> multi-area coverage" : "Broad regional -> national-scale options"
        };
    }

    private static List<string> BuildMediaMix(string bandCode, decimal budget)
    {
        var normalizedCode = bandCode.Trim().ToLowerInvariant();

        return normalizedCode switch
        {
            "launch" => budget <= 35000m
                ? new List<string> { "1-2 local outdoor or digital placements", "Starter radio support", "Single-area visibility" }
                : new List<string> { "1-2 stronger local placements", "Small mixed-media route", "More concentrated local reach" },
            "boost" => budget <= 90000m
                ? new List<string> { "2-3 media items", "Outdoor footprint plus radio support", "Selected multi-area coverage" }
                : new List<string> { "2-4 media items", "Stronger outdoor and radio balance", "Broader regional visibility" },
            "scale" => budget <= 275000m
                ? new List<string> { "Multi-channel media mix", "Balanced radio plus outdoor support", "Regional campaign coverage" }
                : new List<string> { "Stronger multi-channel plan", "Higher frequency support", "Broader target-zone coverage" },
            _ => budget <= 1200000m
                ? new List<string> { "Premium outdoor placements", "Radio support in key markets", "Broader regional reach" }
                : new List<string> { "Premium outdoor, radio, and selected digital placements", "Multi-region or national-scale options", "Higher-frequency exposure" }
        };
    }

    private static List<string> DeserializeList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new List<string>();
        }

        return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
    }

    private static string NormalizeSelectedArea(string? selectedArea)
    {
        var normalized = selectedArea?.Trim().ToLowerInvariant();
        return normalized switch
        {
            "gauteng" => "gauteng",
            "western-cape" => "western-cape",
            "eastern-cape" => "eastern-cape",
            "national" => "national",
            _ => "gauteng"
        };
    }

    private static string HumanizeSelectedArea(string selectedArea)
    {
        return selectedArea switch
        {
            "gauteng" => "Gauteng",
            "western-cape" => "Western Cape",
            "eastern-cape" => "Eastern Cape",
            "national" => "National",
            _ => "Gauteng"
        };
    }

    private sealed class OohPreviewRow
    {
        public string Suburb { get; set; } = string.Empty;

        public string City { get; set; } = string.Empty;

        public string Province { get; set; } = string.Empty;

        public string? RegionClusterCode { get; set; }

        public string SiteName { get; set; } = string.Empty;

        public decimal Cost { get; set; }

        public long TrafficCount { get; set; }
    }

    private sealed class RadioPreviewRow
    {
        public Guid SourceId { get; set; }

        public string StationName { get; set; } = string.Empty;

        public string InventoryName { get; set; } = string.Empty;

        public string Daypart { get; set; } = string.Empty;

        public string InventoryKind { get; set; } = string.Empty;

        public decimal Cost { get; set; }

        public string? GeographyScope { get; set; }

        public string? RegionClusterCode { get; set; }

        public string MarketScope { get; set; } = string.Empty;

        public string MarketTier { get; set; } = string.Empty;

        public int MonthlyListenership { get; set; }

        public decimal BrandStrengthScore { get; set; }

        public decimal CoverageScore { get; set; }

        public decimal AudiencePowerScore { get; set; }

        public string PrimaryAudience { get; set; } = string.Empty;

        public bool IsFlagshipStation { get; set; }

        public bool IsPremiumStation { get; set; }

        public string? ReferenceShowName { get; set; }

        public string? AudienceSummary { get; set; }

        public string? SourceUrl { get; set; }
    }

    private sealed class RadioPreviewCandidate
    {
        public RadioPreviewCandidate(RadioPreviewRow row, decimal score)
        {
            Row = row;
            Score = score;
        }

        public RadioPreviewRow Row { get; }

        public decimal Score { get; }
    }

    private sealed class RadioShowPreviewRow
    {
        public string StationName { get; set; } = string.Empty;

        public string ShowName { get; set; } = string.Empty;

        public string Daypart { get; set; } = string.Empty;
    }

    private sealed class RadioDocumentPreviewRow
    {
        public string SourceName { get; set; } = string.Empty;

        public string DocumentTitle { get; set; } = string.Empty;
    }

    private sealed class TvPreviewRow
    {
        public Guid SourceId { get; set; }

        public string ChannelName { get; set; } = string.Empty;

        public string ProgrammeName { get; set; } = string.Empty;

        public string Daypart { get; set; } = string.Empty;

        public string Genre { get; set; } = string.Empty;

        public decimal Cost { get; set; }

        public string? AudienceSummary { get; set; }
    }
}
