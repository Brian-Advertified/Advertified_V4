using Advertified.App.Services.Abstractions;
using Dapper;
using Npgsql;
using System.Text.Json;

namespace Advertified.App.Services;

public sealed class BroadcastInventoryCatalog : IBroadcastInventoryCatalog
{
    private readonly Npgsql.NpgsqlDataSource _dataSource;
    private readonly SemaphoreSlim _loadLock = new(1, 1);
    private IReadOnlyList<BroadcastInventoryRecord>? _cachedRecords;

    public BroadcastInventoryCatalog(Npgsql.NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task<IReadOnlyList<BroadcastInventoryRecord>> GetRecordsAsync(CancellationToken cancellationToken)
    {
        if (_cachedRecords is not null)
        {
            return _cachedRecords;
        }

        await _loadLock.WaitAsync(cancellationToken);
        try
        {
            if (_cachedRecords is not null)
            {
                return _cachedRecords;
            }

            await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
            var outlets = (await connection.QueryAsync<MediaOutletRow>(
                new CommandDefinition(
                    @"
                    select
                        id,
                        code,
                        name,
                        media_type as MediaType,
                        coverage_type as CoverageType,
                        catalog_health as CatalogHealth,
                        is_national as IsNational,
                        has_pricing as HasPricing,
                        language_notes as LanguageNotes,
                        audience_age_skew as AudienceAgeSkew,
                        audience_gender_skew as AudienceGenderSkew,
                        audience_lsm_range as AudienceLsmRange,
                        audience_racial_skew as AudienceRacialSkew,
                        audience_urban_rural as UrbanRuralMix,
                        broadcast_frequency as BroadcastFrequency,
                        listenership_daily as ListenershipDaily,
                        listenership_weekly as ListenershipWeekly,
                        listenership_period as ListenershipPeriod,
                        target_audience as TargetAudience,
                        data_source_enrichment as DataSourceEnrichment,
                        strategy_fit_json as StrategyFitJson
                    from media_outlet
                    order by media_type, name;
                    ",
                    cancellationToken: cancellationToken)))
                .ToList();

            if (outlets.Count == 0)
            {
                _cachedRecords = Array.Empty<BroadcastInventoryRecord>();
                return _cachedRecords;
            }

            var outletIds = outlets.Select(static outlet => outlet.Id).ToArray();

            var keywords = (await connection.QueryAsync<KeywordRow>(
                new CommandDefinition(
                    "select media_outlet_id as MediaOutletId, keyword from media_outlet_keyword where media_outlet_id = any(@Ids);",
                    new { Ids = outletIds },
                    cancellationToken: cancellationToken)))
                .GroupBy(static row => row.MediaOutletId)
                .ToDictionary(static group => group.Key, static group => group.Select(static row => row.Keyword).ToList());

            var languages = (await connection.QueryAsync<LanguageRow>(
                new CommandDefinition(
                    @"
                    select
                        media_outlet_id as MediaOutletId,
                        language_code as LanguageCode,
                        is_primary as IsPrimary
                    from media_outlet_language
                    where media_outlet_id = any(@Ids)
                    order by is_primary desc, language_code;
                    ",
                    new { Ids = outletIds },
                    cancellationToken: cancellationToken)))
                .GroupBy(static row => row.MediaOutletId)
                .ToDictionary(static group => group.Key, static group => group.ToList());

            var geographies = (await connection.QueryAsync<GeographyRow>(
                new CommandDefinition(
                    @"
                    select
                        media_outlet_id as MediaOutletId,
                        province_code as ProvinceCode,
                        city_name as CityName,
                        geography_type as GeographyType
                    from media_outlet_geography
                    where media_outlet_id = any(@Ids);
                    ",
                    new { Ids = outletIds },
                    cancellationToken: cancellationToken)))
                .GroupBy(static row => row.MediaOutletId)
                .ToDictionary(static group => group.Key, static group => group.ToList());

            var packages = (await connection.QueryAsync<PackageRow>(
                new CommandDefinition(
                    @"
                    select
                        media_outlet_id as MediaOutletId,
                        package_name as PackageName,
                        package_type as PackageType,
                        exposure_count as ExposureCount,
                        monthly_exposure_count as MonthlyExposureCount,
                        value_zar as ValueZar,
                        discount_zar as DiscountZar,
                        saving_zar as SavingZar,
                        investment_zar as InvestmentZar,
                        cost_per_month_zar as CostPerMonthZar,
                        duration_months as DurationMonths,
                        duration_weeks as DurationWeeks,
                        notes
                    from media_outlet_pricing_package
                    where media_outlet_id = any(@Ids) and is_active = true
                    order by package_name;
                    ",
                    new { Ids = outletIds },
                    cancellationToken: cancellationToken)))
                .GroupBy(static row => row.MediaOutletId)
                .ToDictionary(static group => group.Key, static group => group.ToList());

            var rates = (await connection.QueryAsync<RateRow>(
                new CommandDefinition(
                    @"
                    select
                        media_outlet_id as MediaOutletId,
                        day_group as DayGroup,
                        start_time as StartTime,
                        end_time as EndTime,
                        ad_duration_seconds as AdDurationSeconds,
                        rate_zar as RateZar,
                        rate_type as RateType
                    from media_outlet_slot_rate
                    where media_outlet_id = any(@Ids) and is_active = true
                    order by day_group, start_time;
                    ",
                    new { Ids = outletIds },
                    cancellationToken: cancellationToken)))
                .GroupBy(static row => row.MediaOutletId)
                .ToDictionary(static group => group.Key, static group => group.ToList());

            _cachedRecords = outlets
                .Select(outlet => BuildRecord(outlet, keywords, languages, geographies, packages, rates))
                .ToList();

            return _cachedRecords;
        }
        finally
        {
            _loadLock.Release();
        }
    }

    public async Task RefreshAsync(CancellationToken cancellationToken)
    {
        await _loadLock.WaitAsync(cancellationToken);
        try
        {
            _cachedRecords = null;
        }
        finally
        {
            _loadLock.Release();
        }

        await GetRecordsAsync(cancellationToken);
    }

    private static BroadcastInventoryRecord BuildRecord(
        MediaOutletRow outlet,
        IReadOnlyDictionary<Guid, List<string>> keywords,
        IReadOnlyDictionary<Guid, List<LanguageRow>> languages,
        IReadOnlyDictionary<Guid, List<GeographyRow>> geographies,
        IReadOnlyDictionary<Guid, List<PackageRow>> packages,
        IReadOnlyDictionary<Guid, List<RateRow>> rates)
    {
        var outletLanguages = languages.TryGetValue(outlet.Id, out var languageRows)
            ? languageRows
            : new List<LanguageRow>();
        var outletGeographies = geographies.TryGetValue(outlet.Id, out var geographyRows)
            ? geographyRows
            : new List<GeographyRow>();

        return new BroadcastInventoryRecord
        {
            Id = outlet.Code,
            Station = outlet.Name,
            MediaType = outlet.MediaType,
            CatalogHealth = outlet.CatalogHealth,
            CoverageType = outlet.CoverageType,
            BroadcastFrequency = outlet.BroadcastFrequency,
            ProvinceCodes = outletGeographies
                .Where(static geography => string.Equals(geography.GeographyType, "province", StringComparison.OrdinalIgnoreCase))
                .Select(static geography => geography.ProvinceCode ?? string.Empty)
                .Where(static province => !string.IsNullOrWhiteSpace(province))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList(),
            CityLabels = outletGeographies
                .Where(static geography => string.Equals(geography.GeographyType, "city", StringComparison.OrdinalIgnoreCase))
                .Select(static geography => geography.CityName ?? string.Empty)
                .Where(static city => !string.IsNullOrWhiteSpace(city))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList(),
            PrimaryLanguages = outletLanguages
                .Select(static language => language.LanguageCode)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList(),
            LanguageDisplay = outletLanguages.Count == 0 ? null : string.Join("/", outletLanguages.Select(static language => language.LanguageCode)),
            LanguageNotes = outlet.LanguageNotes,
            ListenershipDaily = outlet.ListenershipDaily,
            ListenershipWeekly = outlet.ListenershipWeekly,
            ListenershipPeriod = outlet.ListenershipPeriod,
            AudienceAgeSkew = outlet.AudienceAgeSkew,
            AudienceGenderSkew = outlet.AudienceGenderSkew,
            AudienceLsmRange = outlet.AudienceLsmRange,
            AudienceRacialSkew = outlet.AudienceRacialSkew,
            UrbanRuralMix = outlet.UrbanRuralMix,
            TargetAudience = outlet.TargetAudience,
            AudienceKeywords = keywords.TryGetValue(outlet.Id, out var keywordValues)
                ? keywordValues
                : new List<string>(),
            BuyingBehaviourFit = GetStrategyFitValue(outlet.StrategyFitJson, "buying_behaviour_fit"),
            PricePositioningFit = GetStrategyFitValue(outlet.StrategyFitJson, "price_positioning_fit"),
            SalesModelFit = GetStrategyFitValue(outlet.StrategyFitJson, "sales_model_fit"),
            ObjectiveFitPrimary = GetStrategyFitValue(outlet.StrategyFitJson, "objective_fit_primary"),
            ObjectiveFitSecondary = GetStrategyFitValue(outlet.StrategyFitJson, "objective_fit_secondary"),
            EnvironmentType = GetStrategyFitValue(outlet.StrategyFitJson, "environment_type"),
            PremiumMassFit = GetStrategyFitValue(outlet.StrategyFitJson, "premium_mass_fit"),
            DataConfidence = GetStrategyFitValue(outlet.StrategyFitJson, "data_confidence"),
            IntelligenceNotes = GetStrategyFitValue(outlet.StrategyFitJson, "intelligence_notes"),
            Packages = CreatePackagesJson(packages.TryGetValue(outlet.Id, out var packageRows) ? packageRows : new List<PackageRow>()),
            Pricing = CreatePricingJson(rates.TryGetValue(outlet.Id, out var rateRows) ? rateRows : new List<RateRow>()),
            DataSourceEnrichment = ParseJsonOrDefault(outlet.DataSourceEnrichment),
            HasPricing = outlet.HasPricing,
            IsNational = outlet.IsNational
        };
    }

    private static JsonElement CreatePackagesJson(List<PackageRow> packages)
    {
        if (packages.Count == 0)
        {
            return JsonDocument.Parse("[]").RootElement.Clone();
        }

        var payload = packages.Select(package => new Dictionary<string, object?>
        {
            ["name"] = package.PackageName,
            ["package_type"] = package.PackageType,
            ["exposure"] = package.ExposureCount,
            ["monthly_exposure_count"] = package.MonthlyExposureCount,
            ["value_zar"] = package.ValueZar,
            ["discount_zar"] = package.DiscountZar,
            ["saving_zar"] = package.SavingZar,
            ["investment_zar"] = package.InvestmentZar,
            ["cost_per_month_zar"] = package.CostPerMonthZar,
            ["duration_months"] = package.DurationMonths,
            ["duration_weeks"] = package.DurationWeeks,
            ["notes"] = package.Notes
        }).ToList();

        return JsonSerializer.SerializeToElement(payload);
    }

    private static JsonElement CreatePricingJson(List<RateRow> rates)
    {
        if (rates.Count == 0)
        {
            return JsonDocument.Parse("[]").RootElement.Clone();
        }

        var payload = rates
            .OrderBy(static rate => rate.DayGroup, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static rate => rate.StartTime)
            .ThenBy(static rate => rate.EndTime)
            .ThenBy(static rate => rate.RateType, StringComparer.OrdinalIgnoreCase)
            .Select(static rate => new Dictionary<string, object?>
            {
                ["group"] = rate.DayGroup,
                ["slot"] = $"{rate.StartTime:HH\\:mm}-{rate.EndTime:HH\\:mm}",
                ["start_time"] = $"{rate.StartTime:HH\\:mm}",
                ["end_time"] = $"{rate.EndTime:HH\\:mm}",
                ["rate_zar"] = rate.RateZar,
                ["rate_type"] = rate.RateType
            })
            .ToList();

        return JsonSerializer.SerializeToElement(payload);
    }

    private static JsonElement ParseJsonOrDefault(string? rawJson)
    {
        if (string.IsNullOrWhiteSpace(rawJson))
        {
            return JsonDocument.Parse("[]").RootElement.Clone();
        }

        try
        {
            return JsonDocument.Parse(rawJson).RootElement.Clone();
        }
        catch (JsonException)
        {
            return JsonSerializer.SerializeToElement(new[] { rawJson });
        }
    }

    private static string? GetStrategyFitValue(string? rawJson, string propertyName)
    {
        if (string.IsNullOrWhiteSpace(rawJson))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(rawJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty(propertyName, out var value))
            {
                return null;
            }

            return value.ValueKind switch
            {
                JsonValueKind.String => value.GetString(),
                _ => value.ToString()
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private sealed class MediaOutletRow
    {
        public Guid Id { get; init; }
        public string Code { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string MediaType { get; init; } = string.Empty;
        public string CoverageType { get; init; } = string.Empty;
        public string CatalogHealth { get; init; } = string.Empty;
        public bool IsNational { get; init; }
        public bool HasPricing { get; init; }
        public string? LanguageNotes { get; init; }
        public string? AudienceAgeSkew { get; init; }
        public string? AudienceGenderSkew { get; init; }
        public string? AudienceLsmRange { get; init; }
        public string? AudienceRacialSkew { get; init; }
        public string? UrbanRuralMix { get; init; }
        public string? BroadcastFrequency { get; init; }
        public long? ListenershipDaily { get; init; }
        public long? ListenershipWeekly { get; init; }
        public string? ListenershipPeriod { get; init; }
        public string? TargetAudience { get; init; }
        public string? DataSourceEnrichment { get; init; }
        public string? StrategyFitJson { get; init; }
    }

    private sealed class KeywordRow
    {
        public Guid MediaOutletId { get; init; }
        public string Keyword { get; init; } = string.Empty;
    }

    private sealed class LanguageRow
    {
        public Guid MediaOutletId { get; init; }
        public string LanguageCode { get; init; } = string.Empty;
        public bool IsPrimary { get; init; }
    }

    private sealed class GeographyRow
    {
        public Guid MediaOutletId { get; init; }
        public string? ProvinceCode { get; init; }
        public string? CityName { get; init; }
        public string GeographyType { get; init; } = string.Empty;
    }

    private sealed class PackageRow
    {
        public Guid MediaOutletId { get; init; }
        public string PackageName { get; init; } = string.Empty;
        public string? PackageType { get; init; }
        public int? ExposureCount { get; init; }
        public int? MonthlyExposureCount { get; init; }
        public decimal? ValueZar { get; init; }
        public decimal? DiscountZar { get; init; }
        public decimal? SavingZar { get; init; }
        public decimal? InvestmentZar { get; init; }
        public decimal? CostPerMonthZar { get; init; }
        public int? DurationMonths { get; init; }
        public int? DurationWeeks { get; init; }
        public string? Notes { get; init; }
    }

    private sealed class RateRow
    {
        public Guid MediaOutletId { get; init; }
        public string DayGroup { get; init; } = string.Empty;
        public TimeOnly StartTime { get; init; }
        public TimeOnly EndTime { get; init; }
        public int AdDurationSeconds { get; init; }
        public decimal RateZar { get; init; }
        public string RateType { get; init; } = string.Empty;
    }
}
