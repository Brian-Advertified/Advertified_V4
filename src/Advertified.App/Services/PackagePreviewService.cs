using Advertified.App.Contracts.Packages;
using Advertified.App.Data;
using Advertified.App.Services.Abstractions;
using Dapper;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Text.Json;

namespace Advertified.App.Services;

public sealed class PackagePreviewService : IPackagePreviewService
{
    private readonly AppDbContext _db;
    private readonly Npgsql.NpgsqlDataSource _dataSource;
    private readonly IBroadcastInventoryCatalog _broadcastInventoryCatalog;
    private readonly IPackagePreviewAreaProfileResolver _areaProfileResolver;
    private readonly IPackagePreviewReachEstimator _reachEstimator;
    private readonly IPackagePreviewOutdoorSelector _outdoorSelector;
    private readonly IPackagePreviewBroadcastSelector _broadcastSelector;
    private readonly IPackagePreviewFormatter _formatter;

    public PackagePreviewService(
        AppDbContext db,
        Npgsql.NpgsqlDataSource dataSource,
        IBroadcastInventoryCatalog broadcastInventoryCatalog,
        IPackagePreviewAreaProfileResolver areaProfileResolver,
        IPackagePreviewReachEstimator reachEstimator,
        IPackagePreviewOutdoorSelector outdoorSelector,
        IPackagePreviewBroadcastSelector broadcastSelector,
        IPackagePreviewFormatter formatter)
    {
        _db = db;
        _dataSource = dataSource;
        _broadcastInventoryCatalog = broadcastInventoryCatalog;
        _areaProfileResolver = areaProfileResolver;
        _reachEstimator = reachEstimator;
        _outdoorSelector = outdoorSelector;
        _broadcastSelector = broadcastSelector;
        _formatter = formatter;
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

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        var resolvedArea = await _areaProfileResolver.ResolveAsync(connection, selectedArea, cancellationToken);
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

        var oohExamples = _outdoorSelector.SelectExamples(oohCandidates, resolvedArea, budget, budgetRatio);
        var outdoorMapCandidates = (await connection.QueryAsync<OohPreviewRow>(
            new CommandDefinition(
                GetOutdoorMapSql(),
                new
                {
                    PoolSize = 250
                },
                cancellationToken: cancellationToken)))
            .ToList();

        var broadcastInventoryRecords = await _broadcastInventoryCatalog.GetRecordsAsync(cancellationToken);
        var radioSupportExamples = _broadcastSelector
            .BuildRadioSupportExamples(broadcastInventoryRecords, resolvedArea, band.Code, budget, budgetRatio)
            .ToList();
        var tvSupportExamples = new List<string>();
        var canShowTvExamples = profile.IncludeTv.Equals("yes", StringComparison.OrdinalIgnoreCase)
            || (profile.IncludeTv.Equals("optional", StringComparison.OrdinalIgnoreCase) && budgetRatio >= 0.55m);
        if (canShowTvExamples)
        {
            tvSupportExamples = _broadcastSelector.BuildTvSupportExamples(broadcastInventoryRecords, resolvedArea, band.Code, budget, budgetRatio).ToList();
        }

        var reachEstimate = _reachEstimator.Estimate(band.Code, budgetRatio, oohExamples.Count, radioSupportExamples.Count);

        return new PackagePreviewResult
        {
            Budget = budget,
            SelectedArea = resolvedArea.Name,
            TierLabel = tier.TierLabel,
            PackagePurpose = profile.PackagePurpose,
            RecommendedSpend = profile.RecommendedSpend,
            ReachEstimate = reachEstimate,
            Coverage = _formatter.GetCoverageLabel(band.Code, budget, band.MinBudget, band.MaxBudget),
            ExampleLocations = _formatter.BuildExampleLocations(oohExamples, resolvedArea).ToList(),
            OutdoorMapPoints = _outdoorSelector.BuildMapPoints(outdoorMapCandidates, resolvedArea).ToList(),
            RadioSupportExamples = radioSupportExamples,
            TvSupportExamples = tvSupportExamples,
            TypicalInclusions = DeserializeList(tier.TypicalInclusionsJson),
            IndicativeMix = DeserializeList(tier.IndicativeMixJson),
            MediaMix = _formatter.BuildMediaMix(band.Code, budget).ToList(),
            Note = "Examples shown are indicative. Final media selection depends on your campaign brief, timing, and availability."
        };
    }

    private static string GetOohPreviewSql() =>
        @"
        select
            coalesce(nullif(oii.suburb, ''), nullif(oii.city, ''), 'Priority location') as Suburb,
            coalesce(nullif(oii.city, ''), nullif(oii.province, ''), 'South Africa') as City,
            coalesce(nullif(oii.province, ''), 'South Africa') as Province,
            coalesce(nullif(oii.metadata_json ->> 'region_cluster_code', ''), '') as RegionClusterCode,
            coalesce(nullif(oii.site_name, ''), 'Premium placement') as SiteName,
            coalesce(
                nullif(oii.metadata_json ->> 'gps_coordinates', ''),
                case
                    when oii.latitude is not null and oii.longitude is not null
                        then concat(oii.latitude, ', ', oii.longitude)
                    else ''
                end
            ) as GpsCoordinates,
            coalesce(
                oii.rate_card_zar,
                nullif(regexp_replace(coalesce(oii.metadata_json ->> 'rate_card_zar', ''), '[^0-9.]', '', 'g'), '')::numeric,
                0
            ) as Cost,
            case
                when oii.traffic_count is not null then oii.traffic_count
                when regexp_replace(coalesce(oii.metadata_json ->> 'traffic_count', ''), '[^0-9]', '', 'g') = '' then 0
                else regexp_replace(coalesce(oii.metadata_json ->> 'traffic_count', ''), '[^0-9]', '', 'g')::bigint
            end as TrafficCount
        from ooh_inventory_intelligence oii
        where coalesce(
            oii.rate_card_zar,
            nullif(regexp_replace(coalesce(oii.metadata_json ->> 'rate_card_zar', ''), '[^0-9.]', '', 'g'), '')::numeric,
            0
        ) <= @PlacementBudget
          and coalesce(
            oii.rate_card_zar,
            nullif(regexp_replace(coalesce(oii.metadata_json ->> 'rate_card_zar', ''), '[^0-9.]', '', 'g'), '')::numeric,
            0
          ) > 0
          and oii.is_active = true
        order by TrafficCount desc, Cost desc, oii.city nulls last, oii.suburb nulls last
        limit @PoolSize;
        ";

    private static string GetOutdoorMapSql() =>
        @"
        select
            coalesce(nullif(oii.suburb, ''), nullif(oii.city, ''), 'Priority location') as Suburb,
            coalesce(nullif(oii.city, ''), nullif(oii.province, ''), 'South Africa') as City,
            coalesce(nullif(oii.province, ''), 'South Africa') as Province,
            coalesce(nullif(oii.metadata_json ->> 'region_cluster_code', ''), '') as RegionClusterCode,
            coalesce(nullif(oii.site_name, ''), 'Premium placement') as SiteName,
            coalesce(
                nullif(oii.metadata_json ->> 'gps_coordinates', ''),
                case
                    when oii.latitude is not null and oii.longitude is not null
                        then concat(oii.latitude, ', ', oii.longitude)
                    else ''
                end
            ) as GpsCoordinates,
            coalesce(
                oii.rate_card_zar,
                nullif(regexp_replace(coalesce(oii.metadata_json ->> 'rate_card_zar', ''), '[^0-9.]', '', 'g'), '')::numeric,
                0
            ) as Cost,
            case
                when oii.traffic_count is not null then oii.traffic_count
                when regexp_replace(coalesce(oii.metadata_json ->> 'traffic_count', ''), '[^0-9]', '', 'g') = '' then 0
                else regexp_replace(coalesce(oii.metadata_json ->> 'traffic_count', ''), '[^0-9]', '', 'g')::bigint
            end as TrafficCount
        from ooh_inventory_intelligence oii
        where oii.is_active = true
          and (
            coalesce(nullif(oii.metadata_json ->> 'gps_coordinates', ''), '') <> ''
            or (oii.latitude is not null and oii.longitude is not null)
          )
        order by TrafficCount desc, oii.city nulls last, oii.suburb nulls last
        limit @PoolSize;
        ";

    private static List<string> BuildRadioSupportExamples(List<RadioPreviewRow> rows)
    {
        return rows
            .Select(BuildRadioSupportLabel)
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
            "launch" => budget <= 65000m
                ? new List<string> { "1-2 local Billboards & Digital Screens or digital placements", "Starter radio support", "Single-area visibility" }
                : new List<string> { "1-2 stronger local placements", "Small mixed-media route", "More concentrated local reach" },
            "boost" => budget <= 250000m
                ? new List<string> { "2-3 media items", "Billboards & Digital Screens footprint plus radio support", "Selected multi-area coverage" }
                : new List<string> { "2-4 media items", "Stronger Billboards & Digital Screens and radio balance", "Broader regional visibility" },
            "scale" => budget <= 750000m
                ? new List<string> { "Multi-channel media mix", "Balanced radio plus Billboards & Digital Screens support", "Regional campaign coverage" }
                : new List<string> { "Stronger multi-channel plan", "Higher frequency support", "Broader target-zone coverage" },
            _ => budget <= 1600000m
                ? new List<string> { "Premium Billboards & Digital Screens placements", "Radio support in key markets", "Broader regional reach" }
                : new List<string> { "Premium Billboards & Digital Screens, radio, and selected digital placements", "Multi-region or national-scale options", "Higher-frequency exposure" }
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

    private static (decimal Latitude, decimal Longitude)? TryParseGpsCoordinates(string? gpsCoordinates)
    {
        if (string.IsNullOrWhiteSpace(gpsCoordinates))
        {
            return null;
        }

        var decimalCoordinates = TryParseDecimalCoordinates(gpsCoordinates);
        if (decimalCoordinates is not null)
        {
            return decimalCoordinates;
        }

        var normalized = gpsCoordinates
            .Trim()
            .Replace("Notes:", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("”", "\"", StringComparison.Ordinal)
            .Replace("“", "\"", StringComparison.Ordinal)
            .Replace("’", "'", StringComparison.Ordinal)
            .Replace("‘", "'", StringComparison.Ordinal);

        var matches = Regex.Matches(normalized, @"(\d{1,3})°(\d{1,2})['′](\d{1,2})[""″]?([NSEW])", RegexOptions.IgnoreCase);
        if (matches.Count < 2)
        {
            return null;
        }

        var latitude = ParseDmsCoordinate(matches[0]);
        var longitude = ParseDmsCoordinate(matches[1]);

        if (latitude is null || longitude is null)
        {
            return null;
        }

        return (latitude.Value, longitude.Value);
    }

    private static decimal? ParseDmsCoordinate(Match match)
    {
        if (!match.Success)
        {
            return null;
        }

        if (!decimal.TryParse(match.Groups[1].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var degrees)
            || !decimal.TryParse(match.Groups[2].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var minutes)
            || !decimal.TryParse(match.Groups[3].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var seconds))
        {
            return null;
        }

        var hemisphere = match.Groups[4].Value.ToUpperInvariant();
        var decimalDegrees = degrees + (minutes / 60m) + (seconds / 3600m);

        if (hemisphere is "S" or "W")
        {
            decimalDegrees *= -1m;
        }

        return decimalDegrees;
    }

    private static (decimal Latitude, decimal Longitude)? TryParseDecimalCoordinates(string rawCoordinates)
    {
        var normalized = rawCoordinates
            .Trim()
            .Replace("Notes:", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("Animation:", string.Empty, StringComparison.OrdinalIgnoreCase);

        var decimalMatch = Regex.Match(
            normalized,
            @"(-?\d{1,2}(?:\.\d+)?)\s*,\s*(-?\d{1,3}(?:\.\d+)?)",
            RegexOptions.IgnoreCase);

        if (!decimalMatch.Success)
        {
            return null;
        }

        if (!decimal.TryParse(decimalMatch.Groups[1].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var latitude)
            || !decimal.TryParse(decimalMatch.Groups[2].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var longitude))
        {
            return null;
        }

        if (latitude is < -90m or > 90m || longitude is < -180m or > 180m)
        {
            return null;
        }

        return (latitude, longitude);
    }

    private sealed class AreaProfile
    {
        public string Code { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public List<string> FallbackExampleLocations { get; set; } = new();

        public List<string> ProvinceTerms { get; set; } = new();

        public List<string> CityTerms { get; set; } = new();

        public List<string> StationTerms { get; set; } = new();
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

    private sealed class BroadcastPackageCandidate
    {
        public string Name { get; set; } = string.Empty;
        public decimal? InvestmentZar { get; set; }
        public decimal? PackageCostZar { get; set; }
        public decimal? CostPerMonthZar { get; set; }
        public int? Exposure { get; set; }
        public int? TotalExposure { get; set; }
        public int? NumberOfSpots { get; set; }
        public string? Notes { get; set; }
    }

    private sealed class BroadcastRateCandidate
    {
        public string GroupName { get; set; } = string.Empty;
        public string SlotLabel { get; set; } = string.Empty;
        public decimal RateZar { get; set; }
        public string? ProgrammeName { get; set; }
    }
}
