using Advertified.App.Configuration;
using Advertified.App.Services.Abstractions;
using Dapper;
using Microsoft.Extensions.Options;
using Npgsql;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Globalization;

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
                        ExposureCount = GetInt(element, "exposure") ?? GetInt(element, "number_of_spots"),
                        MonthlyExposureCount = GetInt(element, "monthly_exposure_count") ?? GetInt(element, "total_exposure") ?? GetInt(element, "number_of_spots"),
                        ValueZar = GetDecimal(element, "value_zar") ?? GetDecimal(element, "total_value_zar") ?? GetDecimal(element, "exposure_value_zar"),
                        DiscountZar = GetDecimal(element, "discount_zar"),
                        SavingZar = GetDecimal(element, "saving_zar"),
                        InvestmentZar = GetDecimal(element, "investment_zar")
                            ?? GetDecimal(element, "total_investment_zar")
                            ?? GetDecimal(element, "package_cost_zar"),
                        CostPerMonthZar = GetDecimal(element, "cost_per_month_zar") ?? GetDecimal(package, "cost_per_month_zar"),
                        DurationMonths = GetInt(element, "duration_months") ?? GetInt(package, "duration_months") ?? GetDurationMonthsFromName(GetString(element, "name")) ?? GetDurationMonthsFromName(packageName),
                        DurationWeeks = GetInt(element, "duration_weeks") ?? GetInt(package, "duration_weeks") ?? GetDurationWeeksFromName(GetString(element, "name")) ?? GetDurationWeeksFromName(packageName),
                        Notes = GetString(element, "notes") ?? GetString(package, "notes") ?? GetString(element, "sem") ?? GetString(package, "sem")
                    };
                }

                continue;
            }

            yield return new ImportPackageRow
            {
                PackageName = packageName,
                PackageType = packageType,
                ExposureCount = GetInt(package, "exposure"),
                MonthlyExposureCount = GetInt(package, "monthly_exposure_count") ?? GetInt(package, "total_exposure") ?? GetInt(package, "number_of_spots"),
                ValueZar = GetDecimal(package, "value_zar") ?? GetDecimal(package, "total_value_zar") ?? GetDecimal(package, "exposure_value_zar"),
                DiscountZar = GetDecimal(package, "discount_zar") ?? GetDecimal(package, "total_discount_zar"),
                SavingZar = GetDecimal(package, "saving_zar"),
                InvestmentZar = GetDecimal(package, "investment_zar") ?? GetDecimal(package, "total_investment_zar") ?? GetDecimal(package, "package_cost_zar"),
                CostPerMonthZar = GetDecimal(package, "cost_per_month_zar"),
                DurationMonths = GetInt(package, "duration_months") ?? GetDurationMonthsFromName(packageName),
                DurationWeeks = GetInt(package, "duration_weeks") ?? GetDurationWeeksFromName(packageName),
                Notes = GetString(package, "notes") ?? GetString(package, "sem")
            };
        }
    }

    private static IEnumerable<ImportRateRow> EnumerateRates(BroadcastInventoryRecord record)
    {
        var emittedAny = false;

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
                    emittedAny = true;
                }
            }
        }
        else if (record.Pricing.ValueKind == JsonValueKind.Array)
        {
            foreach (var row in record.Pricing.EnumerateArray())
            {
                if (!TryReadRate(
                        row.TryGetProperty("price_zar", out var price) ? price :
                        row.TryGetProperty("rate_zar", out var rateProperty) ? rateProperty :
                        default,
                        out var rate))
                {
                    continue;
                }

                var slotText = GetString(row, "slot") ?? GetString(row, "time_band") ?? string.Empty;
                if (!TryParseBroadcastSlot(slotText, out var dayGroup, out var start, out var end))
                {
                    dayGroup = "any";
                    start = TimeSpan.Zero;
                    end = new TimeSpan(23, 59, 0);
                }

                var rateType = GetString(row, "program") ?? "spot";
                var adDurationSeconds = GetInt(row, "ad_duration_seconds") ?? 30;

                yield return new ImportRateRow
                {
                    DayGroup = dayGroup,
                    StartTime = start,
                    EndTime = end,
                    AdDurationSeconds = adDurationSeconds,
                    RateZar = rate,
                    RateType = rateType
                };
                emittedAny = true;
            }
        }

        if (!emittedAny)
        {
            foreach (var fallback in EnumerateSportFallbackRates(record))
            {
                yield return fallback;
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
            JsonValueKind.String => TryParseDecimalFlexible(element.GetString(), out rate),
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
            JsonValueKind.String when TryParseDecimalFlexible(property.GetString(), out var value) => value,
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

    private static bool TryParseDecimalFlexible(string? raw, out decimal value)
    {
        value = 0m;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var normalized = raw.Trim().Replace("R", string.Empty, StringComparison.OrdinalIgnoreCase);
        return decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out value)
            || decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.CurrentCulture, out value);
    }

    private static bool TryParseBroadcastSlot(string slot, out string dayGroup, out TimeSpan start, out TimeSpan end)
    {
        dayGroup = "any";
        start = TimeSpan.Zero;
        end = new TimeSpan(23, 59, 0);

        if (string.IsNullOrWhiteSpace(slot))
        {
            return false;
        }

        var normalized = string.Join(' ', slot.Split(' ', StringSplitOptions.RemoveEmptyEntries)).Trim();
        if (TryParseSlotRange(normalized, out start, out end))
        {
            return true;
        }

        var parts = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return false;
        }

        var timeToken = parts[^1];
        if (!TimeSpan.TryParse(timeToken, out start))
        {
            return false;
        }

        dayGroup = parts.Length > 1 ? string.Join(' ', parts.Take(parts.Length - 1)) : "any";
        end = start.Add(TimeSpan.FromMinutes(30));
        if (end > new TimeSpan(23, 59, 0))
        {
            end = new TimeSpan(23, 59, 0);
        }

        return true;
    }

    private static IEnumerable<ImportRateRow> EnumerateSportFallbackRates(BroadcastInventoryRecord record)
    {
        if (!record.MediaType.Equals("tv", StringComparison.OrdinalIgnoreCase))
        {
            yield break;
        }

        if (!record.Station.Contains("SABC Sport", StringComparison.OrdinalIgnoreCase))
        {
            yield break;
        }

        var estimatedRate = ResolveSportFallbackRate(record);
        if (estimatedRate <= 0m)
        {
            yield break;
        }

        foreach (var program in SportFallbackPrograms)
        {
            yield return new ImportRateRow
            {
                DayGroup = "schedule",
                StartTime = TimeSpan.Zero,
                EndTime = new TimeSpan(23, 59, 0),
                AdDurationSeconds = 30,
                RateZar = estimatedRate,
                RateType = program
            };
        }
    }

    private static decimal ResolveSportFallbackRate(BroadcastInventoryRecord record)
    {
        if (record.Packages.ValueKind == JsonValueKind.Array)
        {
            foreach (var package in record.Packages.EnumerateArray())
            {
                var average = GetDecimal(package, "average_cost_per_spot_zar");
                if (average.HasValue && average.Value > 0m)
                {
                    return average.Value;
                }
            }
        }

        return 3000m;
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

    private static int? GetDurationMonthsFromName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        if (name.Contains("12 Months", StringComparison.OrdinalIgnoreCase)) return 12;
        if (name.Contains("6 Months", StringComparison.OrdinalIgnoreCase)) return 6;
        if (name.Contains("3 Months", StringComparison.OrdinalIgnoreCase)) return 3;
        if (name.Contains("1 Month", StringComparison.OrdinalIgnoreCase)) return 1;
        return null;
    }

    private static int? GetDurationWeeksFromName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        if (name.Contains("8 Week", StringComparison.OrdinalIgnoreCase)) return 8;
        if (name.Contains("4 Week", StringComparison.OrdinalIgnoreCase) || name.Contains("4-Week", StringComparison.OrdinalIgnoreCase)) return 4;
        if (name.Contains("2 Week", StringComparison.OrdinalIgnoreCase)) return 2;
        return null;
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

    private static readonly string[] SportFallbackPrograms =
    {
        "Soccerzone",
        "Soccerzone Extra",
        "Top 14 H/Ls",
        "The Greatest Of All Times",
        "Liverpool TV",
        "TotalEnergies AFCON Qualifiers",
        "Sport Playback",
        "Hollywoodbets Super League",
        "CAF CCC / CCL",
        "Retro Match",
        "Sports Wrap",
        "Final Analysis",
        "Boxing World Weekly",
        "Racing Today",
        "Sportsbuzz",
        "Fut Afrique",
        "NBA Action",
        "Redbull Clip Show",
        "Bundesliga Review",
        "Game On",
        "G.O.A.Ts Like Us",
        "TKO Boxing",
        "Redbull Soapbox",
        "Sports Preview 411",
        "Redbull Doccies",
        "Soccer Premier League (2000/1)",
        "NBA Match",
        "Redbull Signature",
        "SAFA TV",
        "Magazine",
        "Premier League Stories",
        "VS Gaming",
        "Laduma",
        "Build Up",
        "Playing For The Coach",
        "NBA Lifestyle",
        "Bundesliga Match",
        "Bundesliga Preview",
        "EFC Live Fight",
        "Redbull Ultimate Rush",
        "Top 14 Rugby"
    };

}
