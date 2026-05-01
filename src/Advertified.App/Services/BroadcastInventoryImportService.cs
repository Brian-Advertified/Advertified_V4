using Advertified.App.Configuration;
using Advertified.App.Services.Abstractions;
using Dapper;
using Microsoft.Extensions.Options;
using Npgsql;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Globalization;
using Advertified.App.Data.Entities;

namespace Advertified.App.Services;

public sealed class BroadcastInventoryImportService : IBroadcastInventoryImportService
{
    private const string BroadcastChannelFamily = "broadcast";

    private readonly Npgsql.NpgsqlDataSource _dataSource;
    private readonly BroadcastInventoryOptions _options;
    private readonly IWebHostEnvironment _environment;
    private readonly IBroadcastInventoryCatalog _broadcastInventoryCatalog;

    public BroadcastInventoryImportService(
        Npgsql.NpgsqlDataSource dataSource,
        IOptions<BroadcastInventoryOptions> options,
        IWebHostEnvironment environment,
        IBroadcastInventoryCatalog broadcastInventoryCatalog)
    {
        _dataSource = dataSource;
        _options = options.Value;
        _environment = environment;
        _broadcastInventoryCatalog = broadcastInventoryCatalog;
    }

    public async Task SyncAsync(CancellationToken cancellationToken)
    {
        var path = ResolveInventoryPath();
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return;
        }

        var fileBytes = await File.ReadAllBytesAsync(path, cancellationToken);
        var checksum = Convert.ToHexString(SHA256.HashData(fileBytes)).ToLowerInvariant();
        await using var stream = new MemoryStream(fileBytes);
        var document = await JsonSerializer.DeserializeAsync<BroadcastInventoryDocument>(
            stream,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
            cancellationToken);

        if (document is null || document.Records.Count == 0)
        {
            return;
        }

        ValidateRequiredFields(document.Records);

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        if (await HasActiveBatchWithChecksumAsync(connection, checksum, cancellationToken))
        {
            return;
        }

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var batchId = Guid.NewGuid();

        await connection.ExecuteAsync(new CommandDefinition(
            @"
            insert into inventory_import_batches (
                id,
                channel_family,
                source_type,
                source_identifier,
                source_checksum,
                record_count,
                status,
                is_active,
                metadata_json,
                activated_at
            )
            values (
                @Id,
                @ChannelFamily,
                'json',
                @SourceIdentifier,
                @SourceChecksum,
                @RecordCount,
                'loading',
                false,
                cast(@MetadataJson as jsonb),
                null
            );
            ",
            new
            {
                Id = batchId,
                ChannelFamily = BroadcastChannelFamily,
                SourceIdentifier = path,
                SourceChecksum = checksum,
                RecordCount = document.Records.Count,
                MetadataJson = JsonSerializer.Serialize(new
                {
                    sourceFileName = Path.GetFileName(path)
                })
            },
            transaction: transaction,
            cancellationToken: cancellationToken));

        foreach (var record in document.Records)
        {
            var importState = await GetOutletImportStateAsync(connection, transaction, record.Id, cancellationToken);
            var outletId = await UpsertOutletAsync(connection, transaction, record, batchId, importState, cancellationToken);
            await ReplaceOutletChildrenAsync(connection, transaction, record, outletId, importState, cancellationToken);
        }

        await ActivateBatchAsync(connection, transaction, batchId, cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        await _broadcastInventoryCatalog.RefreshAsync(cancellationToken);
    }

    private static async Task<bool> HasActiveBatchWithChecksumAsync(
        NpgsqlConnection connection,
        string checksum,
        CancellationToken cancellationToken)
    {
        var activeChecksum = await connection.QuerySingleOrDefaultAsync<string?>(new CommandDefinition(
            @"
            select source_checksum
            from inventory_import_batches
            where channel_family = @ChannelFamily
              and is_active = true
            order by activated_at desc nulls last, created_at desc
            limit 1;
            ",
            new { ChannelFamily = BroadcastChannelFamily },
            cancellationToken: cancellationToken));

        return string.Equals(activeChecksum, checksum, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<OutletImportState> GetOutletImportStateAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string outletCode,
        CancellationToken cancellationToken)
    {
        return await connection.QuerySingleOrDefaultAsync<OutletImportState>(new CommandDefinition(
            @"
            select
                preserve_imported_core_metadata as PreserveImportedCoreMetadata,
                preserve_imported_languages as PreserveImportedLanguages,
                preserve_imported_geography as PreserveImportedGeography,
                preserve_imported_keywords as PreserveImportedKeywords
            from media_outlet
            where code = @Code;
            ",
            new { Code = outletCode },
            transaction: transaction,
            cancellationToken: cancellationToken)) ?? new OutletImportState();
    }

    private static async Task<Guid> UpsertOutletAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        BroadcastInventoryRecord record,
        Guid batchId,
        OutletImportState importState,
        CancellationToken cancellationToken)
    {
        var outletId = CreateDeterministicGuid(record.Id);
        return await connection.ExecuteScalarAsync<Guid>(new CommandDefinition(
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
                data_source_enrichment,
                import_batch_id
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
                @DataSourceEnrichment,
                @ImportBatchId
            )
            on conflict (code) do update
            set
                name = excluded.name,
                media_type = excluded.media_type,
                coverage_type = case
                    when coalesce(media_outlet.preserve_imported_core_metadata, false) then media_outlet.coverage_type
                    else excluded.coverage_type
                end,
                catalog_health = case
                    when coalesce(media_outlet.preserve_imported_core_metadata, false) then media_outlet.catalog_health
                    else excluded.catalog_health
                end,
                operator_name = coalesce(media_outlet.operator_name, excluded.operator_name),
                is_national = case
                    when coalesce(media_outlet.preserve_imported_core_metadata, false) then media_outlet.is_national
                    else excluded.is_national
                end,
                has_pricing = excluded.has_pricing,
                language_notes = case
                    when coalesce(media_outlet.preserve_imported_core_metadata, false) then media_outlet.language_notes
                    else excluded.language_notes
                end,
                audience_age_skew = case
                    when coalesce(media_outlet.preserve_imported_core_metadata, false) then media_outlet.audience_age_skew
                    else excluded.audience_age_skew
                end,
                audience_gender_skew = case
                    when coalesce(media_outlet.preserve_imported_core_metadata, false) then media_outlet.audience_gender_skew
                    else excluded.audience_gender_skew
                end,
                audience_lsm_range = case
                    when coalesce(media_outlet.preserve_imported_core_metadata, false) then media_outlet.audience_lsm_range
                    else excluded.audience_lsm_range
                end,
                audience_racial_skew = case
                    when coalesce(media_outlet.preserve_imported_core_metadata, false) then media_outlet.audience_racial_skew
                    else excluded.audience_racial_skew
                end,
                audience_urban_rural = case
                    when coalesce(media_outlet.preserve_imported_core_metadata, false) then media_outlet.audience_urban_rural
                    else excluded.audience_urban_rural
                end,
                broadcast_frequency = case
                    when coalesce(media_outlet.preserve_imported_core_metadata, false) then media_outlet.broadcast_frequency
                    else excluded.broadcast_frequency
                end,
                listenership_daily = excluded.listenership_daily,
                listenership_weekly = excluded.listenership_weekly,
                listenership_period = excluded.listenership_period,
                target_audience = case
                    when coalesce(media_outlet.preserve_imported_core_metadata, false) then media_outlet.target_audience
                    else excluded.target_audience
                end,
                data_source_enrichment = excluded.data_source_enrichment,
                import_batch_id = excluded.import_batch_id,
                updated_at = now()
            returning id;
            ",
            new
            {
                Id = outletId,
                Code = record.Id,
                Name = record.Station,
                MediaType = record.MediaType,
                CoverageType = NormalizeCoverageType(record.CoverageType),
                CatalogHealth = NormalizeReferenceCode(record.CatalogHealth),
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
                ImportBatchId = batchId,
                DataSourceEnrichment = record.DataSourceEnrichment.ValueKind == JsonValueKind.Undefined
                    ? null
                    : record.DataSourceEnrichment.GetRawText()
            },
            transaction: transaction,
            cancellationToken: cancellationToken));
    }

    private static async Task ReplaceOutletChildrenAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        BroadcastInventoryRecord record,
        Guid outletId,
        OutletImportState importState,
        CancellationToken cancellationToken)
    {
        await connection.ExecuteAsync(new CommandDefinition(
            @"
            delete from media_outlet_pricing_package where media_outlet_id = @MediaOutletId;
            delete from media_outlet_slot_rate where media_outlet_id = @MediaOutletId;
            ",
            new { MediaOutletId = outletId },
            transaction: transaction,
            cancellationToken: cancellationToken));

        if (!importState.PreserveImportedKeywords)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                "delete from media_outlet_keyword where media_outlet_id = @MediaOutletId;",
                new { MediaOutletId = outletId },
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
        }

        if (!importState.PreserveImportedLanguages)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                "delete from media_outlet_language where media_outlet_id = @MediaOutletId;",
                new { MediaOutletId = outletId },
                transaction: transaction,
                cancellationToken: cancellationToken));

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
        }

        if (!importState.PreserveImportedGeography)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                "delete from media_outlet_geography where media_outlet_id = @MediaOutletId;",
                new { MediaOutletId = outletId },
                transaction: transaction,
                cancellationToken: cancellationToken));

            foreach (var province in record.ProvinceCodes
                .Select(NormalizeReferenceCode)
                .Where(static value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase))
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
                        ProvinceCode = province
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

    private static async Task ActivateBatchAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid batchId,
        CancellationToken cancellationToken)
    {
        await connection.ExecuteAsync(new CommandDefinition(
            @"
            update inventory_import_batches
            set
                is_active = false,
                status = case when status = 'active' then 'superseded' else status end
            where channel_family = @ChannelFamily
              and id <> @BatchId
              and is_active = true;

            update inventory_import_batches
            set
                status = 'active',
                is_active = true,
                activated_at = now()
            where id = @BatchId;
            ",
            new
            {
                ChannelFamily = BroadcastChannelFamily,
                BatchId = batchId
            },
            transaction: transaction,
            cancellationToken: cancellationToken));
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
            var packageType = GetString(package, "package_type") ?? InferPackageType(packageName, GetString(package, "notes"));

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

    private static string NormalizeReferenceCode(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().Replace("-", "_").Replace(" ", "_").ToLowerInvariant();
    }

    private static string NormalizeCoverageType(string? value)
    {
        var normalized = NormalizeReferenceCode(value);
        return normalized == "provincial" ? "regional" : normalized;
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

    private static void ValidateRequiredFields(IReadOnlyList<BroadcastInventoryRecord> records)
    {
        var issues = new List<string>();

        foreach (var record in records)
        {
            if (string.IsNullOrWhiteSpace(record.Id))
            {
                issues.Add("A broadcast record is missing its id.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(record.Station))
            {
                issues.Add($"{record.Id}: missing station name.");
            }

            if (!string.Equals(record.MediaType, "radio", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(record.MediaType, "tv", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(record.MediaType, "newspaper", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(record.MediaType, "print", StringComparison.OrdinalIgnoreCase))
            {
                issues.Add($"{record.Id}: invalid media_type '{record.MediaType}'.");
            }

            if (string.IsNullOrWhiteSpace(record.CoverageType))
            {
                issues.Add($"{record.Id}: missing coverage_type.");
            }

            if (string.IsNullOrWhiteSpace(record.CatalogHealth))
            {
                issues.Add($"{record.Id}: missing catalog_health.");
            }

            if (!record.IsNational && record.ProvinceCodes.Count == 0 && record.CityLabels.Count == 0)
            {
                issues.Add($"{record.Id}: missing geography for non-national outlet.");
            }

            if (record.PrimaryLanguages.Count == 0)
            {
                issues.Add($"{record.Id}: missing primary_languages.");
            }

            if (string.IsNullOrWhiteSpace(record.TargetAudience))
            {
                issues.Add($"{record.Id}: missing target_audience.");
            }
        }

        if (issues.Count == 0)
        {
            return;
        }

        throw new InvalidOperationException(
            "Broadcast inventory import contains incomplete records: " +
            string.Join(" | ", issues.Take(12)) +
            (issues.Count > 12 ? $" | +{issues.Count - 12} more" : string.Empty));
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

    private sealed class OutletImportState
    {
        public bool PreserveImportedCoreMetadata { get; init; }
        public bool PreserveImportedLanguages { get; init; }
        public bool PreserveImportedGeography { get; init; }
        public bool PreserveImportedKeywords { get; init; }
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
