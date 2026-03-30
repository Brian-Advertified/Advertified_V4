using Advertified.App.Configuration;
using Advertified.App.Services.Abstractions;
using Dapper;
using Microsoft.Extensions.Options;
using Npgsql;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Advertified.App.Services;

public sealed class BroadcastInventoryImportService : IBroadcastInventoryImportService
{
    private readonly string _connectionString;
    private readonly BroadcastInventoryOptions _options;
    private readonly IWebHostEnvironment _environment;

    public BroadcastInventoryImportService(
        string connectionString,
        IOptions<BroadcastInventoryOptions> options,
        IWebHostEnvironment environment)
    {
        _connectionString = connectionString;
        _options = options.Value;
        _environment = environment;
    }

    public async Task SyncAsync(CancellationToken cancellationToken)
    {
        var path = ResolveInventoryPath();
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return;
        }

        await using var stream = File.OpenRead(path);
        var document = await JsonSerializer.DeserializeAsync<BroadcastInventoryDocument>(
            stream,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
            cancellationToken);

        if (document is null || document.Records.Count == 0)
        {
            return;
        }

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await connection.ExecuteAsync(new CommandDefinition(
            @"
            delete from media_outlet
            where lower(media_type) in ('radio', 'tv');
            ",
            transaction: transaction,
            cancellationToken: cancellationToken));

        foreach (var record in document.Records)
        {
            var outletId = CreateDeterministicGuid(record.Id);
            await connection.ExecuteAsync(new CommandDefinition(
                @"
                insert into media_outlet (
                    id,
                    code,
                    name,
                    media_type,
                    coverage_type,
                    catalog_health,
                    operator_name,
                    is_national,
                    has_pricing,
                    language_notes,
                    audience_age_skew,
                    audience_gender_skew,
                    audience_lsm_range,
                    audience_racial_skew,
                    audience_urban_rural,
                    broadcast_frequency,
                    listenership_daily,
                    listenership_weekly,
                    listenership_period,
                    target_audience,
                    data_source_enrichment
                )
                values (
                    @Id,
                    @Code,
                    @Name,
                    @MediaType,
                    @CoverageType,
                    @CatalogHealth,
                    @OperatorName,
                    @IsNational,
                    @HasPricing,
                    @LanguageNotes,
                    @AudienceAgeSkew,
                    @AudienceGenderSkew,
                    @AudienceLsmRange,
                    @AudienceRacialSkew,
                    @AudienceUrbanRural,
                    @BroadcastFrequency,
                    @ListenershipDaily,
                    @ListenershipWeekly,
                    @ListenershipPeriod,
                    @TargetAudience,
                    @DataSourceEnrichment
                );
                ",
                new
                {
                    Id = outletId,
                    Code = record.Id,
                    Name = record.Station,
                    MediaType = record.MediaType,
                    CoverageType = record.CoverageType,
                    CatalogHealth = record.CatalogHealth,
                    OperatorName = (string?)null,
                    IsNational = record.IsNational,
                    HasPricing = record.HasPricing,
                    record.LanguageNotes,
                    record.AudienceAgeSkew,
                    record.AudienceGenderSkew,
                    record.AudienceLsmRange,
                    record.AudienceRacialSkew,
                    AudienceUrbanRural = record.UrbanRuralMix,
                    record.BroadcastFrequency,
                    record.ListenershipDaily,
                    record.ListenershipWeekly,
                    record.ListenershipPeriod,
                    record.TargetAudience,
                    DataSourceEnrichment = record.DataSourceEnrichment.ValueKind == JsonValueKind.Undefined
                        ? null
                        : record.DataSourceEnrichment.GetRawText()
                },
                transaction: transaction,
                cancellationToken: cancellationToken));

            for (var index = 0; index < record.AudienceKeywords.Count; index++)
            {
                var keyword = record.AudienceKeywords[index]?.Trim();
                if (string.IsNullOrWhiteSpace(keyword))
                {
                    continue;
                }

                await connection.ExecuteAsync(new CommandDefinition(
                    "insert into media_outlet_keyword (id, media_outlet_id, keyword) values (@Id, @MediaOutletId, @Keyword);",
                    new
                    {
                        Id = CreateDeterministicGuid($"{record.Id}:keyword:{keyword}"),
                        MediaOutletId = outletId,
                        Keyword = keyword
                    },
                    transaction: transaction,
                    cancellationToken: cancellationToken));
            }

            for (var index = 0; index < record.PrimaryLanguages.Count; index++)
            {
                var language = record.PrimaryLanguages[index]?.Trim().ToLowerInvariant();
                if (string.IsNullOrWhiteSpace(language))
                {
                    continue;
                }

                await connection.ExecuteAsync(new CommandDefinition(
                    "insert into media_outlet_language (id, media_outlet_id, language_code, is_primary) values (@Id, @MediaOutletId, @LanguageCode, @IsPrimary);",
                    new
                    {
                        Id = CreateDeterministicGuid($"{record.Id}:language:{language}"),
                        MediaOutletId = outletId,
                        LanguageCode = language,
                        IsPrimary = index == 0
                    },
                    transaction: transaction,
                    cancellationToken: cancellationToken));
            }

            foreach (var province in record.ProvinceCodes.Where(static value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                await connection.ExecuteAsync(new CommandDefinition(
                    @"
                    insert into media_outlet_geography (id, media_outlet_id, province_code, city_name, geography_type)
                    values (@Id, @MediaOutletId, @ProvinceCode, null, 'province');
                    ",
                    new
                    {
                        Id = CreateDeterministicGuid($"{record.Id}:province:{province}"),
                        MediaOutletId = outletId,
                        ProvinceCode = province.Trim().ToLowerInvariant()
                    },
                    transaction: transaction,
                    cancellationToken: cancellationToken));
            }

            foreach (var city in record.CityLabels.Where(static value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                await connection.ExecuteAsync(new CommandDefinition(
                    @"
                    insert into media_outlet_geography (id, media_outlet_id, province_code, city_name, geography_type)
                    values (@Id, @MediaOutletId, null, @CityName, 'city');
                    ",
                    new
                    {
                        Id = CreateDeterministicGuid($"{record.Id}:city:{city}"),
                        MediaOutletId = outletId,
                        CityName = city.Trim()
                    },
                    transaction: transaction,
                    cancellationToken: cancellationToken));
            }

            foreach (var package in EnumeratePackages(record))
            {
                await connection.ExecuteAsync(new CommandDefinition(
                    @"
                    insert into media_outlet_pricing_package (
                        id,
                        media_outlet_id,
                        package_name,
                        package_type,
                        exposure_count,
                        monthly_exposure_count,
                        value_zar,
                        discount_zar,
                        saving_zar,
                        investment_zar,
                        cost_per_month_zar,
                        duration_months,
                        duration_weeks,
                        notes
                    )
                    values (
                        @Id,
                        @MediaOutletId,
                        @PackageName,
                        @PackageType,
                        @ExposureCount,
                        @MonthlyExposureCount,
                        @ValueZar,
                        @DiscountZar,
                        @SavingZar,
                        @InvestmentZar,
                        @CostPerMonthZar,
                        @DurationMonths,
                        @DurationWeeks,
                        @Notes
                    );
                    ",
                    new
                    {
                        Id = CreateDeterministicGuid($"{record.Id}:package:{package.PackageName}"),
                        MediaOutletId = outletId,
                        package.PackageName,
                        package.PackageType,
                        package.ExposureCount,
                        package.MonthlyExposureCount,
                        package.ValueZar,
                        package.DiscountZar,
                        package.SavingZar,
                        package.InvestmentZar,
                        package.CostPerMonthZar,
                        package.DurationMonths,
                        package.DurationWeeks,
                        package.Notes
                    },
                    transaction: transaction,
                    cancellationToken: cancellationToken));
            }

            foreach (var rate in EnumerateRates(record))
            {
                await connection.ExecuteAsync(new CommandDefinition(
                    @"
                    insert into media_outlet_slot_rate (
                        id,
                        media_outlet_id,
                        day_group,
                        start_time,
                        end_time,
                        ad_duration_seconds,
                        rate_zar,
                        rate_type
                    )
                    values (
                        @Id,
                        @MediaOutletId,
                        @DayGroup,
                        @StartTime,
                        @EndTime,
                        @AdDurationSeconds,
                        @RateZar,
                        @RateType
                    );
                    ",
                    new
                    {
                        Id = CreateDeterministicGuid($"{record.Id}:rate:{rate.DayGroup}:{rate.StartTime}:{rate.EndTime}:{rate.RateType}"),
                        MediaOutletId = outletId,
                        rate.DayGroup,
                        rate.StartTime,
                        rate.EndTime,
                        rate.AdDurationSeconds,
                        rate.RateZar,
                        rate.RateType
                    },
                    transaction: transaction,
                    cancellationToken: cancellationToken));
            }

        }

        await transaction.CommitAsync(cancellationToken);
    }

    private string? ResolveInventoryPath()
    {
        var configured = _options.NormalizedInventoryPath;
        if (string.IsNullOrWhiteSpace(configured))
        {
            return null;
        }

        return Path.IsPathRooted(configured)
            ? configured
            : Path.Combine(_environment.ContentRootPath, configured);
    }

    private static IEnumerable<ImportPackageRow> EnumeratePackages(BroadcastInventoryRecord record)
    {
        if (record.Packages.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        foreach (var package in record.Packages.EnumerateArray())
        {
            var packageName = GetString(package, "name") ?? "Package";
            var packageType = InferPackageType(packageName, GetString(package, "notes"));

            if (package.TryGetProperty("elements", out var elements) && elements.ValueKind == JsonValueKind.Array)
            {
                foreach (var element in elements.EnumerateArray())
                {
                    yield return new ImportPackageRow
                    {
                        PackageName = $"{packageName} - {GetString(element, "name") ?? "Element"}",
                        PackageType = packageType,
                        ExposureCount = GetInt(element, "exposure"),
                        MonthlyExposureCount = GetInt(element, "monthly_exposure_count") ?? GetInt(element, "total_exposure"),
                        ValueZar = GetDecimal(element, "value_zar"),
                        DiscountZar = GetDecimal(element, "discount_zar"),
                        SavingZar = GetDecimal(element, "saving_zar"),
                        InvestmentZar = GetDecimal(element, "investment_zar"),
                        CostPerMonthZar = GetDecimal(package, "cost_per_month_zar"),
                        DurationMonths = GetInt(package, "duration_months"),
                        DurationWeeks = GetInt(package, "duration_weeks"),
                        Notes = GetString(element, "notes") ?? GetString(package, "notes")
                    };
                }

                continue;
            }

            yield return new ImportPackageRow
            {
                PackageName = packageName,
                PackageType = packageType,
                ExposureCount = GetInt(package, "exposure"),
                MonthlyExposureCount = GetInt(package, "monthly_exposure_count") ?? GetInt(package, "total_exposure"),
                ValueZar = GetDecimal(package, "value_zar") ?? GetDecimal(package, "total_value_zar"),
                DiscountZar = GetDecimal(package, "discount_zar") ?? GetDecimal(package, "total_discount_zar"),
                SavingZar = GetDecimal(package, "saving_zar"),
                InvestmentZar = GetDecimal(package, "investment_zar") ?? GetDecimal(package, "total_investment_zar"),
                CostPerMonthZar = GetDecimal(package, "cost_per_month_zar"),
                DurationMonths = GetInt(package, "duration_months"),
                DurationWeeks = GetInt(package, "duration_weeks"),
                Notes = GetString(package, "notes")
            };
        }
    }

    private static IEnumerable<ImportRateRow> EnumerateRates(BroadcastInventoryRecord record)
    {
        if (record.Pricing.ValueKind == JsonValueKind.Object)
        {
            foreach (var group in record.Pricing.EnumerateObject())
            {
                if (group.Value.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                foreach (var slot in group.Value.EnumerateObject())
                {
                    if (!TryReadRate(slot.Value, out var rate) || !TryParseSlotRange(slot.Name, out var start, out var end))
                    {
                        continue;
                    }

                    yield return new ImportRateRow
                    {
                        DayGroup = group.Name,
                        StartTime = start,
                        EndTime = end,
                        AdDurationSeconds = 30,
                        RateZar = rate,
                        RateType = "spot"
                    };
                }
            }
        }
    }

    private static bool TryParseSlotRange(string slot, out TimeSpan start, out TimeSpan end)
    {
        start = default;
        end = default;
        var parts = slot.Split('-', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 2
            && TimeSpan.TryParse(parts[0], out start)
            && TimeSpan.TryParse(parts[1], out end);
    }

    private static bool TryReadRate(JsonElement element, out decimal rate)
    {
        rate = 0m;
        return element.ValueKind switch
        {
            JsonValueKind.Number => element.TryGetDecimal(out rate),
            JsonValueKind.String => decimal.TryParse(element.GetString(), out rate),
            _ => false
        };
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

        return property.ValueKind switch
        {
            JsonValueKind.Number when property.TryGetDecimal(out var number) => number,
            JsonValueKind.String when decimal.TryParse(property.GetString(), out var value) => value,
            _ => null
        };
    }

    private static int? GetInt(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        return property.ValueKind switch
        {
            JsonValueKind.Number when property.TryGetInt32(out var number) => number,
            JsonValueKind.String when int.TryParse(property.GetString(), out var value) => value,
            _ => null
        };
    }

    private static string InferPackageType(string? packageName, string? notes)
    {
        var text = $"{packageName} {notes}".ToLowerInvariant();
        if (text.Contains("sponsorship"))
        {
            return "sponsorship";
        }

        if (text.Contains("pre-roll") || text.Contains("preroll"))
        {
            return "preroll";
        }

        if (text.Contains("mixed"))
        {
            return "mixed";
        }

        return "generic";
    }

    private static Guid CreateDeterministicGuid(string input)
    {
        var bytes = MD5.HashData(Encoding.UTF8.GetBytes(input));
        return new Guid(bytes);
    }

    private sealed class BroadcastInventoryDocument
    {
        public List<BroadcastInventoryRecord> Records { get; set; } = new();
    }

    private sealed class ImportPackageRow
    {
        public string PackageName { get; init; } = string.Empty;
        public string PackageType { get; init; } = string.Empty;
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

    private sealed class ImportRateRow
    {
        public string DayGroup { get; init; } = string.Empty;
        public TimeSpan StartTime { get; init; }
        public TimeSpan EndTime { get; init; }
        public int AdDurationSeconds { get; init; }
        public decimal RateZar { get; init; }
        public string RateType { get; init; } = string.Empty;
    }

}
