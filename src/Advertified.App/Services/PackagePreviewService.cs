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
    private readonly string _connectionString;
    private readonly IBroadcastInventoryCatalog _broadcastInventoryCatalog;
    private readonly IPackagePreviewAreaProfileResolver _areaProfileResolver;
    private readonly IPackagePreviewReachEstimator _reachEstimator;
    private readonly IPackagePreviewOutdoorSelector _outdoorSelector;
    private readonly IPackagePreviewBroadcastSelector _broadcastSelector;
    private readonly IPackagePreviewFormatter _formatter;

    public PackagePreviewService(
        AppDbContext db,
        string connectionString,
        IBroadcastInventoryCatalog broadcastInventoryCatalog,
        IPackagePreviewAreaProfileResolver areaProfileResolver,
        IPackagePreviewReachEstimator reachEstimator,
        IPackagePreviewOutdoorSelector outdoorSelector,
        IPackagePreviewBroadcastSelector broadcastSelector,
        IPackagePreviewFormatter formatter)
    {
        _db = db;
        _connectionString = connectionString;
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

        await using var connection = new NpgsqlConnection(_connectionString);
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
            coalesce(nullif(iif.suburb, ''), nullif(iif.city, ''), 'Priority location') as Suburb,
            coalesce(nullif(iif.city, ''), nullif(iif.province, ''), 'South Africa') as City,
            coalesce(nullif(iif.province, ''), 'South Africa') as Province,
            coalesce(rc.code, '') as RegionClusterCode,
            coalesce(nullif(iif.site_name, ''), 'Premium placement') as SiteName,
            coalesce(nullif(iif.metadata_json ->> 'gps_coordinates', ''), '') as GpsCoordinates,
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

    private static string GetOutdoorMapSql() =>
        @"
        select
            coalesce(nullif(iif.suburb, ''), nullif(iif.city, ''), 'Priority location') as Suburb,
            coalesce(nullif(iif.city, ''), nullif(iif.province, ''), 'South Africa') as City,
            coalesce(nullif(iif.province, ''), 'South Africa') as Province,
            coalesce(rc.code, '') as RegionClusterCode,
            coalesce(nullif(iif.site_name, ''), 'Premium placement') as SiteName,
            coalesce(nullif(iif.metadata_json ->> 'gps_coordinates', ''), '') as GpsCoordinates,
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
        where coalesce(nullif(iif.metadata_json ->> 'gps_coordinates', ''), '') <> ''
        order by TrafficCount desc, iif.city nulls last, iif.suburb nulls last
        limit @PoolSize;
        ";

    private static bool RadioMatchesArea(RadioPreviewRow row, AreaProfile selectedArea)
    {
        var clusterCode = NormalizeBroadcastToken(row.RegionClusterCode ?? string.Empty);
        var selectedCode = NormalizeBroadcastToken(selectedArea.Code);
        if (!string.IsNullOrWhiteSpace(clusterCode))
        {
            return selectedCode switch
            {
                "national" => true,
                "kzn" => clusterCode is "kzn" or "kwazulu_natal" or "national",
                _ => clusterCode == selectedCode || clusterCode == "national"
            };
        }

        var geography = row.GeographyScope?.Trim().ToLowerInvariant() ?? string.Empty;
        var station = row.StationName.Trim().ToLowerInvariant();

        return selectedArea.ProvinceTerms.Any(geography.Contains)
            || selectedArea.CityTerms.Any(geography.Contains)
            || selectedArea.StationTerms.Any(station.Contains);
    }

    private static List<RadioPreviewRow> SelectRadioExamples(List<RadioPreviewRow> candidates, AreaProfile selectedArea, string bandCode, decimal budget, decimal budgetRatio)
    {
        if (candidates.Count == 0)
        {
            return new List<RadioPreviewRow>();
        }

        var normalizedBandCode = bandCode.Trim().ToLowerInvariant();
        var filteredCandidates = selectedArea.Code == "national"
            ? candidates
            : candidates.Where(candidate => RadioMatchesArea(candidate, selectedArea)).ToList();
        if (filteredCandidates.Count == 0)
        {
            return new List<RadioPreviewRow>();
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


    private static List<string> BuildRadioSupportExamplesFromBroadcastInventory(
        IReadOnlyList<BroadcastInventoryRecord> records,
        AreaProfile selectedArea,
        string bandCode,
        decimal budget,
        decimal budgetRatio)
    {
        var normalizedBandCode = bandCode.Trim().ToLowerInvariant();
        var candidates = records
            .Where(record => string.Equals(record.MediaType, "radio", StringComparison.OrdinalIgnoreCase))
            .Where(record => BroadcastRecordMatchesArea(record, selectedArea) || IsNationalRecordAllowed(record, normalizedBandCode))
            .Select(record => new
            {
                Record = record,
                Score = ScoreBroadcastRecordForPreview(record, selectedArea, normalizedBandCode, budget, budgetRatio, isTv: false)
            })
            .OrderByDescending(candidate => candidate.Score)
            .ThenByDescending(candidate => candidate.Record.HasPricing)
            .ThenByDescending(candidate => candidate.Record.ListenershipWeekly ?? candidate.Record.ListenershipDaily ?? 0)
            .ThenBy(candidate => candidate.Record.Station, StringComparer.OrdinalIgnoreCase)
            .Take(8)
            .ToList();

        return candidates
            .Select(candidate => BuildRadioSupportLabel(candidate.Record, budget))
            .Where(static label => !string.IsNullOrWhiteSpace(label))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(3)
            .ToList();
    }

    private static List<string> BuildTvSupportExamplesFromBroadcastInventory(
        IReadOnlyList<BroadcastInventoryRecord> records,
        AreaProfile selectedArea,
        string bandCode,
        decimal budget,
        decimal budgetRatio)
    {
        var normalizedBandCode = bandCode.Trim().ToLowerInvariant();
        var candidates = records
            .Where(record => string.Equals(record.MediaType, "tv", StringComparison.OrdinalIgnoreCase))
            .Where(record => selectedArea.Code == "national" || record.IsNational || string.Equals(record.CoverageType, "national", StringComparison.OrdinalIgnoreCase))
            .Select(record => new
            {
                Record = record,
                Score = ScoreBroadcastRecordForPreview(record, selectedArea, normalizedBandCode, budget, budgetRatio, isTv: true)
            })
            .OrderByDescending(candidate => candidate.Score)
            .ThenByDescending(candidate => candidate.Record.HasPricing)
            .ThenBy(candidate => candidate.Record.Station, StringComparer.OrdinalIgnoreCase)
            .Take(6)
            .ToList();

        var labels = candidates
            .Select(candidate => BuildTvSupportLabel(candidate.Record, budget))
            .Where(static label => !string.IsNullOrWhiteSpace(label))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(selectedArea.Code == "national" ? 3 : 2)
            .ToList();

        if (labels.Count == 0)
        {
            return labels;
        }

        if (selectedArea.Code != "national")
        {
            labels.Insert(0, "TV can be included for broader or national campaigns at this package level");
        }

        return labels
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(3)
            .ToList();
    }

    private static List<RadioPreviewRow> BuildRadioPreviewRowsFromBroadcastInventory(IReadOnlyList<BroadcastInventoryRecord> records)
    {
        return records
            .Where(record => string.Equals(record.MediaType, "radio", StringComparison.OrdinalIgnoreCase))
            .SelectMany(CreateRadioPreviewRows)
            .ToList();
    }

    private static List<TvPreviewRow> BuildTvPreviewRowsFromBroadcastInventory(IReadOnlyList<BroadcastInventoryRecord> records)
    {
        return records
            .Where(record => string.Equals(record.MediaType, "tv", StringComparison.OrdinalIgnoreCase))
            .SelectMany(CreateTvPreviewRows)
            .ToList();
    }

    private static IEnumerable<RadioPreviewRow> CreateRadioPreviewRows(BroadcastInventoryRecord record)
    {
        var rows = new List<RadioPreviewRow>();
        var geographyScope = BuildGeographyScope(record);
        var sourceUrl = TryGetFirstSourceUrl(record);
        var packageCandidates = EnumeratePackageCandidates(record.Packages).ToList();
        var rateCandidates = EnumerateRateCandidates(record.Pricing).ToList();

        foreach (var package in packageCandidates)
        {
            var cost = package.InvestmentZar ?? package.PackageCostZar ?? package.CostPerMonthZar ?? 0m;
            rows.Add(new RadioPreviewRow
            {
                SourceId = CreateDeterministicGuid($"{record.Id}:radio:package:{package.Name}"),
                StationName = record.Station,
                InventoryName = package.Name,
                Daypart = DeriveDaypartFromText(package.Name),
                InventoryKind = "package",
                Cost = cost,
                GeographyScope = geographyScope,
                RegionClusterCode = record.ProvinceCodes.FirstOrDefault(),
                MarketScope = record.CoverageType,
                MarketTier = record.CatalogHealth,
                MonthlyListenership = GetMonthlyListenership(record),
                BrandStrengthScore = GetBrandStrengthScore(record),
                CoverageScore = GetCoverageScore(record),
                AudiencePowerScore = GetAudiencePowerScore(record),
                PrimaryAudience = record.TargetAudience ?? string.Empty,
                IsFlagshipStation = record.IsNational || string.Equals(record.CatalogHealth, "strong", StringComparison.OrdinalIgnoreCase),
                IsPremiumStation = string.Equals(record.CatalogHealth, "strong", StringComparison.OrdinalIgnoreCase),
                ReferenceShowName = package.Name,
                AudienceSummary = record.TargetAudience,
                SourceUrl = sourceUrl
            });
        }

        foreach (var rate in rateCandidates)
        {
            rows.Add(new RadioPreviewRow
            {
                SourceId = CreateDeterministicGuid($"{record.Id}:radio:rate:{rate.GroupName}:{rate.SlotLabel}:{rate.ProgrammeName}"),
                StationName = record.Station,
                InventoryName = string.IsNullOrWhiteSpace(rate.ProgrammeName) ? $"{record.Station} slot" : rate.ProgrammeName!,
                Daypart = string.IsNullOrWhiteSpace(rate.SlotLabel) ? rate.GroupName : rate.SlotLabel,
                InventoryKind = "rate_card",
                Cost = rate.RateZar,
                GeographyScope = geographyScope,
                RegionClusterCode = record.ProvinceCodes.FirstOrDefault(),
                MarketScope = record.CoverageType,
                MarketTier = record.CatalogHealth,
                MonthlyListenership = GetMonthlyListenership(record),
                BrandStrengthScore = GetBrandStrengthScore(record),
                CoverageScore = GetCoverageScore(record),
                AudiencePowerScore = GetAudiencePowerScore(record),
                PrimaryAudience = record.TargetAudience ?? string.Empty,
                IsFlagshipStation = record.IsNational || string.Equals(record.CatalogHealth, "strong", StringComparison.OrdinalIgnoreCase),
                IsPremiumStation = string.Equals(record.CatalogHealth, "strong", StringComparison.OrdinalIgnoreCase),
                ReferenceShowName = rate.ProgrammeName,
                AudienceSummary = record.TargetAudience,
                SourceUrl = sourceUrl
            });
        }

        if (rows.Count == 0)
        {
            rows.Add(new RadioPreviewRow
            {
                SourceId = CreateDeterministicGuid($"{record.Id}:radio:fallback"),
                StationName = record.Station,
                InventoryName = $"{record.Station} support",
                Daypart = "selected dayparts",
                InventoryKind = "station",
                Cost = 0m,
                GeographyScope = geographyScope,
                RegionClusterCode = record.ProvinceCodes.FirstOrDefault(),
                MarketScope = record.CoverageType,
                MarketTier = record.CatalogHealth,
                MonthlyListenership = GetMonthlyListenership(record),
                BrandStrengthScore = GetBrandStrengthScore(record),
                CoverageScore = GetCoverageScore(record),
                AudiencePowerScore = GetAudiencePowerScore(record),
                PrimaryAudience = record.TargetAudience ?? string.Empty,
                IsFlagshipStation = record.IsNational,
                IsPremiumStation = string.Equals(record.CatalogHealth, "strong", StringComparison.OrdinalIgnoreCase),
                ReferenceShowName = null,
                AudienceSummary = record.TargetAudience,
                SourceUrl = sourceUrl
            });
        }

        return rows;
    }

    private static IEnumerable<TvPreviewRow> CreateTvPreviewRows(BroadcastInventoryRecord record)
    {
        var rows = new List<TvPreviewRow>();
        var packageCandidates = EnumeratePackageCandidates(record.Packages).ToList();
        var rateCandidates = EnumerateRateCandidates(record.Pricing).ToList();

        foreach (var package in packageCandidates)
        {
            rows.Add(new TvPreviewRow
            {
                SourceId = CreateDeterministicGuid($"{record.Id}:tv:package:{package.Name}"),
                ChannelName = record.Station,
                ProgrammeName = package.Name,
                Daypart = DeriveDaypartFromText(package.Name),
                Genre = DeriveGenreFromText(package.Name, record.TargetAudience),
                Cost = package.InvestmentZar ?? package.PackageCostZar ?? package.CostPerMonthZar ?? 0m,
                AudienceSummary = record.TargetAudience
            });
        }

        foreach (var rate in rateCandidates)
        {
            rows.Add(new TvPreviewRow
            {
                SourceId = CreateDeterministicGuid($"{record.Id}:tv:rate:{rate.GroupName}:{rate.SlotLabel}:{rate.ProgrammeName}"),
                ChannelName = record.Station,
                ProgrammeName = rate.ProgrammeName ?? $"{record.Station} placement",
                Daypart = rate.SlotLabel,
                Genre = DeriveGenreFromText(rate.ProgrammeName, record.TargetAudience),
                Cost = rate.RateZar,
                AudienceSummary = record.TargetAudience
            });
        }

        if (rows.Count == 0)
        {
            rows.Add(new TvPreviewRow
            {
                SourceId = CreateDeterministicGuid($"{record.Id}:tv:fallback"),
                ChannelName = record.Station,
                ProgrammeName = $"{record.Station} support",
                Daypart = "selected slots",
                Genre = DeriveGenreFromText(record.Station, record.TargetAudience),
                Cost = 0m,
                AudienceSummary = record.TargetAudience
            });
        }

        return rows;
    }

    private static bool BroadcastRecordMatchesArea(BroadcastInventoryRecord record, AreaProfile selectedArea)
    {
        if (selectedArea.Code == "national")
        {
            return record.IsNational || string.Equals(record.CoverageType, "national", StringComparison.OrdinalIgnoreCase);
        }

        var selectedCode = NormalizeBroadcastToken(selectedArea.Code);
        var provinces = record.ProvinceCodes.Select(NormalizeBroadcastToken).ToList();
        var cities = record.CityLabels.Select(static city => city.Trim().ToLowerInvariant()).ToList();

        if (selectedCode == "kzn" && provinces.Contains("kwazulu_natal", StringComparer.OrdinalIgnoreCase))
        {
            return true;
        }

        if (provinces.Contains(selectedCode, StringComparer.OrdinalIgnoreCase))
        {
            return true;
        }

        return selectedArea.CityTerms.Any(term => cities.Any(city => city.Contains(term, StringComparison.OrdinalIgnoreCase)));
    }

    private static bool IsNationalRecordAllowed(BroadcastInventoryRecord record, string bandCode)
    {
        return (bandCode == "scale" || bandCode == "dominance")
            && (record.IsNational || string.Equals(record.CoverageType, "national", StringComparison.OrdinalIgnoreCase));
    }

    private static decimal ScoreBroadcastRecordForPreview(
        BroadcastInventoryRecord record,
        AreaProfile selectedArea,
        string bandCode,
        decimal budget,
        decimal budgetRatio,
        bool isTv)
    {
        var score = 0m;

        if (BroadcastRecordMatchesArea(record, selectedArea))
        {
            score += 18m;
        }
        else if (record.IsNational || string.Equals(record.CoverageType, "national", StringComparison.OrdinalIgnoreCase))
        {
            score += bandCode is "scale" or "dominance" ? 10m : 2m;
        }

        if (record.HasPricing)
        {
            score += 8m;
        }

        score += record.CatalogHealth switch
        {
            "strong" => 8m,
            "mixed" => 4m,
            "weak_partial_pricing" => 2m,
            _ => 0m
        };

        score += string.Equals(record.CoverageType, "national", StringComparison.OrdinalIgnoreCase)
            ? (bandCode is "scale" or "dominance" ? 5m : -1m)
            : string.Equals(record.CoverageType, "regional", StringComparison.OrdinalIgnoreCase)
                ? 3m
                : 1m;

        if (record.ListenershipWeekly.HasValue)
        {
            score += Math.Min(10m, record.ListenershipWeekly.Value / 150000m);
        }
        else if (record.ListenershipDaily.HasValue)
        {
            score += Math.Min(8m, record.ListenershipDaily.Value / 75000m);
        }

        if (!isTv)
        {
            if (record.AudienceKeywords.Any(keyword => keyword.Contains("music", StringComparison.OrdinalIgnoreCase)
                || keyword.Contains("lifestyle", StringComparison.OrdinalIgnoreCase)
                || keyword.Contains("commuter", StringComparison.OrdinalIgnoreCase)))
            {
                score += 3m;
            }
        }
        else if (record.AudienceKeywords.Any(keyword => keyword.Contains("news", StringComparison.OrdinalIgnoreCase)
            || keyword.Contains("sport", StringComparison.OrdinalIgnoreCase)))
        {
            score += 3m;
        }

        var pricePoint = GetClosestSpendPoint(record, budget, isTv);
        if (pricePoint.HasValue && pricePoint.Value > 0m)
        {
            var target = isTv ? budget * 0.35m : budget * 0.18m;
            var ratio = target <= 0m ? 1m : pricePoint.Value / target;
            if (ratio <= 1.1m)
            {
                score += 6m;
            }
            else if (ratio <= 1.4m)
            {
                score += 3m;
            }
        }
        else if (budgetRatio < 0.5m)
        {
            score -= 2m;
        }

        return score;
    }

    private static decimal? GetClosestSpendPoint(BroadcastInventoryRecord record, decimal budget, bool isTv)
    {
        var candidates = EnumeratePackageCandidates(record.Packages)
            .Select(package => package.InvestmentZar ?? package.PackageCostZar ?? package.CostPerMonthZar)
            .Concat(EnumerateRateCandidates(record.Pricing).Select(rate => (decimal?)rate.RateZar))
            .Where(value => value.HasValue && value.Value > 0m)
            .Select(value => value!.Value)
            .ToList();

        if (candidates.Count == 0)
        {
            return null;
        }

        var target = isTv ? budget * 0.35m : budget * 0.18m;
        return candidates
            .OrderBy(value => Math.Abs(value - target))
            .First();
    }

    private static string BuildRadioSupportLabel(BroadcastInventoryRecord record, decimal budget)
    {
        var package = EnumeratePackageCandidates(record.Packages)
            .OrderBy(package => Math.Abs((package.InvestmentZar ?? package.PackageCostZar ?? package.CostPerMonthZar ?? 0m) - (budget * 0.18m)))
            .FirstOrDefault(package => (package.InvestmentZar ?? package.PackageCostZar ?? package.CostPerMonthZar ?? 0m) > 0m);

        if (package is not null)
        {
            return $"{record.Station} - {package.Name} ({DescribeBroadcastAudience(record)})";
        }

        var rate = EnumerateRateCandidates(record.Pricing)
            .OrderBy(rate => Math.Abs(rate.RateZar - (budget * 0.12m)))
            .FirstOrDefault();

        if (rate is not null)
        {
            var slot = string.IsNullOrWhiteSpace(rate.ProgrammeName) ? HumanizeDaypart(rate.SlotLabel) : rate.ProgrammeName!;
            return $"{record.Station} - {slot} ({DescribeBroadcastAudience(record)})";
        }

        return $"{record.Station} - station support ({DescribeBroadcastAudience(record)})";
    }

    private static string BuildTvSupportLabel(BroadcastInventoryRecord record, decimal budget)
    {
        var package = EnumeratePackageCandidates(record.Packages)
            .OrderBy(package => Math.Abs((package.InvestmentZar ?? package.PackageCostZar ?? package.CostPerMonthZar ?? 0m) - (budget * 0.35m)))
            .FirstOrDefault(package => (package.InvestmentZar ?? package.PackageCostZar ?? package.CostPerMonthZar ?? 0m) > 0m);

        if (package is not null)
        {
            return $"{record.Station} - {package.Name} ({DescribeBroadcastAudience(record)})";
        }

        var rate = EnumerateRateCandidates(record.Pricing)
            .OrderBy(rate => Math.Abs(rate.RateZar - (budget * 0.22m)))
            .FirstOrDefault();

        if (rate is not null)
        {
            var programme = rate.ProgrammeName ?? rate.SlotLabel;
            return $"{record.Station} - {programme} ({DescribeBroadcastAudience(record)})";
        }

        return $"{record.Station} - channel support ({DescribeBroadcastAudience(record)})";
    }

    private static string DescribeBroadcastAudience(BroadcastInventoryRecord record)
    {
        if (!string.IsNullOrWhiteSpace(record.TargetAudience))
        {
            var lowered = record.TargetAudience.Trim().ToLowerInvariant();
            if (lowered.Contains("business"))
            {
                return "business audience";
            }

            if (lowered.Contains("lifestyle"))
            {
                return "lifestyle audience";
            }

            if (lowered.Contains("youth"))
            {
                return "youth audience";
            }

            if (lowered.Contains("community"))
            {
                return "community audience";
            }
        }

        if (record.AudienceKeywords.Any(keyword => keyword.Contains("news", StringComparison.OrdinalIgnoreCase)))
        {
            return "news audience";
        }

        if (record.AudienceKeywords.Any(keyword => keyword.Contains("sport", StringComparison.OrdinalIgnoreCase)))
        {
            return "sport audience";
        }

        if (record.AudienceKeywords.Any(keyword => keyword.Contains("lifestyle", StringComparison.OrdinalIgnoreCase)))
        {
            return "lifestyle audience";
        }

        return "selected audience fit";
    }

    private static IEnumerable<BroadcastPackageCandidate> EnumeratePackageCandidates(JsonElement packages)
    {
        if (packages.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        foreach (var item in packages.EnumerateArray())
        {
            var candidate = new BroadcastPackageCandidate
            {
                Name = GetString(item, "name") ?? "Package",
                InvestmentZar = GetDecimal(item, "investment_zar"),
                PackageCostZar = GetDecimal(item, "package_cost_zar"),
                CostPerMonthZar = GetDecimal(item, "cost_per_month_zar"),
                Exposure = GetInt(item, "exposure"),
                TotalExposure = GetInt(item, "total_exposure"),
                NumberOfSpots = GetInt(item, "number_of_spots"),
                Notes = GetString(item, "notes")
            };

            if (item.TryGetProperty("elements", out var elements) && elements.ValueKind == JsonValueKind.Array)
            {
                foreach (var element in elements.EnumerateArray())
                {
                    yield return new BroadcastPackageCandidate
                    {
                        Name = $"{candidate.Name} - {GetString(element, "name") ?? "Element"}",
                        InvestmentZar = GetDecimal(element, "investment_zar"),
                        PackageCostZar = GetDecimal(element, "package_cost_zar"),
                        CostPerMonthZar = candidate.CostPerMonthZar,
                        Exposure = GetInt(element, "exposure"),
                        TotalExposure = GetInt(element, "total_exposure"),
                        NumberOfSpots = GetInt(element, "number_of_spots"),
                        Notes = GetString(element, "notes")
                    };
                }

                continue;
            }

            yield return candidate;
        }
    }

    private static IEnumerable<BroadcastRateCandidate> EnumerateRateCandidates(JsonElement pricing)
    {
        if (pricing.ValueKind == JsonValueKind.Object)
        {
            foreach (var group in pricing.EnumerateObject())
            {
                if (group.Value.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                foreach (var slot in group.Value.EnumerateObject())
                {
                    var rate = slot.Value.ValueKind switch
                    {
                        JsonValueKind.Number when slot.Value.TryGetDecimal(out var numberRate) => numberRate,
                        JsonValueKind.String when decimal.TryParse(slot.Value.GetString(), out var stringRate) => stringRate,
                        _ => 0m
                    };

                    if (rate <= 0m)
                    {
                        continue;
                    }

                    yield return new BroadcastRateCandidate
                    {
                        GroupName = group.Name,
                        SlotLabel = slot.Name,
                        RateZar = rate
                    };
                }
            }
        }
        else if (pricing.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in pricing.EnumerateArray())
            {
                var rate = GetDecimal(item, "price_zar") ?? GetDecimal(item, "rate_zar") ?? 0m;
                if (rate <= 0m)
                {
                    continue;
                }

                yield return new BroadcastRateCandidate
                {
                    GroupName = GetString(item, "group") ?? "schedule",
                    SlotLabel = GetString(item, "slot") ?? GetString(item, "time") ?? "selected slot",
                    RateZar = rate,
                    ProgrammeName = GetString(item, "program") ?? GetString(item, "programme")
                };
            }
        }
    }

    private static string BuildGeographyScope(BroadcastInventoryRecord record)
    {
        var tokens = record.CityLabels
            .Concat(record.ProvinceCodes)
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return tokens.Count == 0
            ? record.CoverageType
            : string.Join(", ", tokens);
    }

    private static string? TryGetFirstSourceUrl(BroadcastInventoryRecord record)
    {
        if (record.Packages.ValueKind == JsonValueKind.Undefined)
        {
            return null;
        }

        return null;
    }

    private static int GetMonthlyListenership(BroadcastInventoryRecord record)
    {
        if (record.ListenershipDaily.HasValue)
        {
            return (int)Math.Min(int.MaxValue, record.ListenershipDaily.Value * 22L);
        }

        if (record.ListenershipWeekly.HasValue)
        {
            return (int)Math.Min(int.MaxValue, record.ListenershipWeekly.Value * 4L);
        }

        return 0;
    }

    private static decimal GetBrandStrengthScore(BroadcastInventoryRecord record)
    {
        return record.CatalogHealth switch
        {
            "strong" => 9m,
            "mixed" => 6m,
            "weak_partial_pricing" => 4m,
            "weak_unpriced" => 2m,
            _ => 3m
        };
    }

    private static decimal GetCoverageScore(BroadcastInventoryRecord record)
    {
        return record.CoverageType switch
        {
            "national" => 10m,
            "regional" => 7m,
            "local" => 5m,
            _ => 4m
        };
    }

    private static decimal GetAudiencePowerScore(BroadcastInventoryRecord record)
    {
        return Math.Min(10m, record.AudienceKeywords.Count + (record.PrimaryLanguages.Count * 0.5m));
    }

    private static string DeriveDaypartFromText(string? source)
    {
        var text = source?.Trim().ToLowerInvariant() ?? string.Empty;
        if (text.Contains("breakfast"))
        {
            return "breakfast";
        }

        if (text.Contains("drive"))
        {
            return "drive";
        }

        if (text.Contains("lunch") || text.Contains("midday"))
        {
            return "midday";
        }

        if (text.Contains("weekend"))
        {
            return "weekend";
        }

        return "selected dayparts";
    }

    private static string DeriveGenreFromText(string? primary, string? secondary)
    {
        var text = $"{primary} {secondary}".Trim().ToLowerInvariant();
        if (text.Contains("sport"))
        {
            return "sport";
        }

        if (text.Contains("news"))
        {
            return "news";
        }

        if (text.Contains("youth"))
        {
            return "youth";
        }

        if (text.Contains("lifestyle"))
        {
            return "lifestyle";
        }

        return "general";
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
    }

    private static decimal? GetDecimal(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        if (property.ValueKind == JsonValueKind.Number && property.TryGetDecimal(out var number))
        {
            return number;
        }

        if (property.ValueKind == JsonValueKind.String
            && decimal.TryParse(property.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        return null;
    }

    private static int? GetInt(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        if (property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out var number))
        {
            return number;
        }

        if (property.ValueKind == JsonValueKind.String
            && int.TryParse(property.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        return null;
    }

    private static Guid CreateDeterministicGuid(string value)
    {
        using var md5 = System.Security.Cryptography.MD5.Create();
        var hash = md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(value));
        return new Guid(hash);
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

    private static string NormalizeBroadcastToken(string value)
    {
        return value.Trim().Replace(" ", "_").Replace("-", "_").ToLowerInvariant();
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
