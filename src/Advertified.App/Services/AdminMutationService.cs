using System.Text.Json;
using Advertified.App.Contracts.Admin;
using Advertified.App.Services.Abstractions;
using Dapper;
using Npgsql;

namespace Advertified.App.Services;

public sealed class AdminMutationService : IAdminMutationService
{
    private readonly string _connectionString;
    private readonly IWebHostEnvironment _environment;

    public AdminMutationService(string connectionString, IWebHostEnvironment environment)
    {
        _connectionString = connectionString;
        _environment = environment;
    }

    public async Task<AdminOutletDetailResponse> GetOutletAsync(string code, CancellationToken cancellationToken)
    {
        var normalizedCode = NormalizeToken(code);
        if (string.IsNullOrWhiteSpace(normalizedCode))
        {
            throw new InvalidOperationException("Outlet code is required.");
        }

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var outlet = await connection.QuerySingleOrDefaultAsync<AdminOutletDetailRecord>(new CommandDefinition(
            @"
            select
                id,
                code,
                name,
                media_type as MediaType,
                coverage_type as CoverageType,
                catalog_health as CatalogHealth,
                operator_name as OperatorName,
                is_national as IsNational,
                has_pricing as HasPricing,
                language_notes as LanguageNotes,
                target_audience as TargetAudience,
                broadcast_frequency as BroadcastFrequency
            from media_outlet
            where code = @Code;",
            new { Code = normalizedCode },
            cancellationToken: cancellationToken));

        if (outlet is null)
        {
            throw new InvalidOperationException("Outlet was not found.");
        }

        var languages = (await connection.QueryAsync<string>(new CommandDefinition(
            "select language_code from media_outlet_language where media_outlet_id = @MediaOutletId order by language_code;",
            new { MediaOutletId = outlet.Id },
            cancellationToken: cancellationToken))).ToArray();

        var geography = (await connection.QueryAsync<AdminOutletGeographyRecord>(new CommandDefinition(
            "select province_code as ProvinceCode, city_name as CityName, geography_type as GeographyType from media_outlet_geography where media_outlet_id = @MediaOutletId order by geography_type, province_code, city_name;",
            new { MediaOutletId = outlet.Id },
            cancellationToken: cancellationToken))).ToArray();

        var keywords = (await connection.QueryAsync<string>(new CommandDefinition(
            "select keyword from media_outlet_keyword where media_outlet_id = @MediaOutletId order by keyword;",
            new { MediaOutletId = outlet.Id },
            cancellationToken: cancellationToken))).ToArray();

        var pricingStats = await connection.QuerySingleAsync<AdminOutletPricingStatsRecord>(new CommandDefinition(
            @"
            select
                (select count(*) from media_outlet_pricing_package where media_outlet_id = @MediaOutletId and is_active = true) as PackageCount,
                (select count(*) from media_outlet_slot_rate where media_outlet_id = @MediaOutletId and is_active = true) as SlotRateCount,
                (select min(coalesce(cost_per_month_zar, investment_zar, value_zar)) from media_outlet_pricing_package where media_outlet_id = @MediaOutletId and is_active = true) as MinPackagePrice,
                (select min(rate_zar) from media_outlet_slot_rate where media_outlet_id = @MediaOutletId and is_active = true) as MinSlotRate;",
            new { MediaOutletId = outlet.Id },
            cancellationToken: cancellationToken));

        return new AdminOutletDetailResponse
        {
            Id = outlet.Id,
            Code = outlet.Code,
            Name = outlet.Name,
            MediaType = outlet.MediaType,
            CoverageType = outlet.CoverageType,
            CatalogHealth = outlet.CatalogHealth,
            OperatorName = outlet.OperatorName,
            IsNational = outlet.IsNational,
            HasPricing = outlet.HasPricing,
            LanguageNotes = outlet.LanguageNotes,
            TargetAudience = outlet.TargetAudience,
            BroadcastFrequency = outlet.BroadcastFrequency,
            PrimaryLanguages = languages,
            ProvinceCodes = geography.Where(x => string.Equals(x.GeographyType, "province", StringComparison.OrdinalIgnoreCase)).Select(x => x.ProvinceCode!).Where(x => !string.IsNullOrWhiteSpace(x)).ToArray(),
            CityLabels = geography.Where(x => string.Equals(x.GeographyType, "city", StringComparison.OrdinalIgnoreCase)).Select(x => x.CityName!).Where(x => !string.IsNullOrWhiteSpace(x)).ToArray(),
            AudienceKeywords = keywords,
            PackageCount = pricingStats.PackageCount,
            SlotRateCount = pricingStats.SlotRateCount,
            MinPackagePrice = pricingStats.MinPackagePrice,
            MinSlotRate = pricingStats.MinSlotRate
        };
    }

    public async Task<AdminOutletPricingResponse> GetOutletPricingAsync(string code, CancellationToken cancellationToken)
    {
        var detail = await GetOutletAsync(code, cancellationToken);

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var packages = (await connection.QueryAsync<AdminOutletPricingPackageResponse>(new CommandDefinition(
            @"
            select
                id,
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
                notes,
                source_name as SourceName,
                source_date as SourceDate,
                is_active as IsActive
            from media_outlet_pricing_package
            where media_outlet_id = @MediaOutletId
            order by is_active desc, package_name;",
            new { MediaOutletId = detail.Id },
            cancellationToken: cancellationToken))).ToArray();

        var slotRates = (await connection.QueryAsync<AdminOutletSlotRateResponse>(new CommandDefinition(
            @"
            select
                id,
                day_group as DayGroup,
                start_time as StartTime,
                end_time as EndTime,
                ad_duration_seconds as AdDurationSeconds,
                rate_zar as RateZar,
                rate_type as RateType,
                source_name as SourceName,
                source_date as SourceDate,
                is_active as IsActive
            from media_outlet_slot_rate
            where media_outlet_id = @MediaOutletId
            order by is_active desc, day_group, start_time;",
            new { MediaOutletId = detail.Id },
            cancellationToken: cancellationToken))).ToArray();

        return new AdminOutletPricingResponse
        {
            OutletCode = detail.Code,
            OutletName = detail.Name,
            MediaType = detail.MediaType,
            CoverageType = detail.CoverageType,
            HasPricing = detail.HasPricing,
            Packages = packages,
            SlotRates = slotRates
        };
    }

    public async Task<AdminOutletMutationResponse> CreateOutletAsync(CreateAdminOutletRequest request, CancellationToken cancellationToken)
    {
        var code = NormalizeToken(request.Code);
        var name = request.Name.Trim();
        ValidateOutletRequest(code, name);

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var exists = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "select count(*) from media_outlet where code = @Code;",
            new { Code = code },
            transaction: transaction,
            cancellationToken: cancellationToken));

        if (exists > 0)
        {
            throw new InvalidOperationException("An outlet with this code already exists.");
        }

        var outletId = Guid.NewGuid();
        await UpsertOutletAsync(connection, transaction, outletId, code, request.Name, request.MediaType, request.CoverageType, request.CatalogHealth, request.OperatorName, request.IsNational, request.HasPricing, request.LanguageNotes, request.TargetAudience, request.BroadcastFrequency, request.PrimaryLanguages, request.ProvinceCodes, request.CityLabels, request.AudienceKeywords, cancellationToken, isUpdate: false);
        await transaction.CommitAsync(cancellationToken);

        return new AdminOutletMutationResponse { Code = code, Name = name };
    }

    public async Task<AdminOutletMutationResponse> UpdateOutletAsync(string existingCode, UpdateAdminOutletRequest request, CancellationToken cancellationToken)
    {
        var normalizedExistingCode = NormalizeToken(existingCode);
        var nextCode = NormalizeToken(request.Code);
        var nextName = request.Name.Trim();
        ValidateOutletRequest(nextCode, nextName);

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var outletId = await connection.ExecuteScalarAsync<Guid?>(new CommandDefinition(
            "select id from media_outlet where code = @Code;",
            new { Code = normalizedExistingCode },
            transaction: transaction,
            cancellationToken: cancellationToken));

        if (!outletId.HasValue)
        {
            throw new InvalidOperationException("Outlet was not found.");
        }

        if (!string.Equals(normalizedExistingCode, nextCode, StringComparison.OrdinalIgnoreCase))
        {
            var codeExists = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                "select count(*) from media_outlet where code = @Code and id <> @Id;",
                new { Code = nextCode, Id = outletId.Value },
                transaction: transaction,
                cancellationToken: cancellationToken));

            if (codeExists > 0)
            {
                throw new InvalidOperationException("Another outlet with this code already exists.");
            }
        }

        await UpsertOutletAsync(connection, transaction, outletId.Value, nextCode, request.Name, request.MediaType, request.CoverageType, request.CatalogHealth, request.OperatorName, request.IsNational, request.HasPricing, request.LanguageNotes, request.TargetAudience, request.BroadcastFrequency, request.PrimaryLanguages, request.ProvinceCodes, request.CityLabels, request.AudienceKeywords, cancellationToken, isUpdate: true);
        await transaction.CommitAsync(cancellationToken);

        return new AdminOutletMutationResponse { Code = nextCode, Name = nextName };
    }

    public async Task DeleteOutletAsync(string code, CancellationToken cancellationToken)
    {
        var normalizedCode = NormalizeToken(code);
        if (string.IsNullOrWhiteSpace(normalizedCode))
        {
            throw new InvalidOperationException("Outlet code is required.");
        }

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var deleted = await connection.ExecuteAsync(new CommandDefinition(
            "delete from media_outlet where code = @Code;",
            new { Code = normalizedCode },
            cancellationToken: cancellationToken));

        if (deleted == 0)
        {
            throw new InvalidOperationException("Outlet was not found.");
        }
    }

    public async Task<Guid> CreateOutletPricingPackageAsync(string code, UpsertAdminOutletPricingPackageRequest request, CancellationToken cancellationToken)
    {
        var outletId = await GetOutletIdAsync(code, cancellationToken);
        var packageId = Guid.NewGuid();

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            @"
            insert into media_outlet_pricing_package (
                id, media_outlet_id, package_name, package_type, exposure_count, monthly_exposure_count,
                value_zar, discount_zar, saving_zar, investment_zar, cost_per_month_zar,
                duration_months, duration_weeks, notes, source_name, source_date, is_active
            ) values (
                @Id, @MediaOutletId, @PackageName, @PackageType, @ExposureCount, @MonthlyExposureCount,
                @ValueZar, @DiscountZar, @SavingZar, @InvestmentZar, @CostPerMonthZar,
                @DurationMonths, @DurationWeeks, @Notes, @SourceName, @SourceDate, @IsActive
            );",
            new
            {
                Id = packageId,
                MediaOutletId = outletId,
                PackageName = request.PackageName.Trim(),
                PackageType = TrimToNull(request.PackageType),
                request.ExposureCount,
                request.MonthlyExposureCount,
                request.ValueZar,
                request.DiscountZar,
                request.SavingZar,
                request.InvestmentZar,
                request.CostPerMonthZar,
                request.DurationMonths,
                request.DurationWeeks,
                Notes = TrimToNull(request.Notes),
                SourceName = TrimToNull(request.SourceName),
                request.SourceDate,
                request.IsActive
            },
            cancellationToken: cancellationToken));

        await UpdateOutletHasPricingFlagAsync(connection, outletId, cancellationToken);
        return packageId;
    }

    public async Task UpdateOutletPricingPackageAsync(string code, Guid packageId, UpsertAdminOutletPricingPackageRequest request, CancellationToken cancellationToken)
    {
        var outletId = await GetOutletIdAsync(code, cancellationToken);

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        var updated = await connection.ExecuteAsync(new CommandDefinition(
            @"
            update media_outlet_pricing_package
            set
                package_name = @PackageName,
                package_type = @PackageType,
                exposure_count = @ExposureCount,
                monthly_exposure_count = @MonthlyExposureCount,
                value_zar = @ValueZar,
                discount_zar = @DiscountZar,
                saving_zar = @SavingZar,
                investment_zar = @InvestmentZar,
                cost_per_month_zar = @CostPerMonthZar,
                duration_months = @DurationMonths,
                duration_weeks = @DurationWeeks,
                notes = @Notes,
                source_name = @SourceName,
                source_date = @SourceDate,
                is_active = @IsActive,
                updated_at = now()
            where id = @Id and media_outlet_id = @MediaOutletId;",
            new
            {
                Id = packageId,
                MediaOutletId = outletId,
                PackageName = request.PackageName.Trim(),
                PackageType = TrimToNull(request.PackageType),
                request.ExposureCount,
                request.MonthlyExposureCount,
                request.ValueZar,
                request.DiscountZar,
                request.SavingZar,
                request.InvestmentZar,
                request.CostPerMonthZar,
                request.DurationMonths,
                request.DurationWeeks,
                Notes = TrimToNull(request.Notes),
                SourceName = TrimToNull(request.SourceName),
                request.SourceDate,
                request.IsActive
            },
            cancellationToken: cancellationToken));

        if (updated == 0)
        {
            throw new InvalidOperationException("Pricing package was not found.");
        }

        await UpdateOutletHasPricingFlagAsync(connection, outletId, cancellationToken);
    }

    public async Task DeleteOutletPricingPackageAsync(string code, Guid packageId, CancellationToken cancellationToken)
    {
        var outletId = await GetOutletIdAsync(code, cancellationToken);
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        var deleted = await connection.ExecuteAsync(new CommandDefinition(
            "delete from media_outlet_pricing_package where id = @Id and media_outlet_id = @MediaOutletId;",
            new { Id = packageId, MediaOutletId = outletId },
            cancellationToken: cancellationToken));

        if (deleted == 0)
        {
            throw new InvalidOperationException("Pricing package was not found.");
        }

        await UpdateOutletHasPricingFlagAsync(connection, outletId, cancellationToken);
    }

    public async Task<Guid> CreateOutletSlotRateAsync(string code, UpsertAdminOutletSlotRateRequest request, CancellationToken cancellationToken)
    {
        var outletId = await GetOutletIdAsync(code, cancellationToken);
        var slotRateId = Guid.NewGuid();
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            @"
            insert into media_outlet_slot_rate (
                id, media_outlet_id, day_group, start_time, end_time, ad_duration_seconds,
                rate_zar, rate_type, source_name, source_date, is_active
            ) values (
                @Id, @MediaOutletId, @DayGroup, @StartTime, @EndTime, @AdDurationSeconds,
                @RateZar, @RateType, @SourceName, @SourceDate, @IsActive
            );",
            new
            {
                Id = slotRateId,
                MediaOutletId = outletId,
                DayGroup = NormalizeToken(request.DayGroup),
                request.StartTime,
                request.EndTime,
                request.AdDurationSeconds,
                request.RateZar,
                RateType = NormalizeToken(request.RateType),
                SourceName = TrimToNull(request.SourceName),
                request.SourceDate,
                request.IsActive
            },
            cancellationToken: cancellationToken));

        await UpdateOutletHasPricingFlagAsync(connection, outletId, cancellationToken);
        return slotRateId;
    }

    public async Task UpdateOutletSlotRateAsync(string code, Guid slotRateId, UpsertAdminOutletSlotRateRequest request, CancellationToken cancellationToken)
    {
        var outletId = await GetOutletIdAsync(code, cancellationToken);
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        var updated = await connection.ExecuteAsync(new CommandDefinition(
            @"
            update media_outlet_slot_rate
            set
                day_group = @DayGroup,
                start_time = @StartTime,
                end_time = @EndTime,
                ad_duration_seconds = @AdDurationSeconds,
                rate_zar = @RateZar,
                rate_type = @RateType,
                source_name = @SourceName,
                source_date = @SourceDate,
                is_active = @IsActive,
                updated_at = now()
            where id = @Id and media_outlet_id = @MediaOutletId;",
            new
            {
                Id = slotRateId,
                MediaOutletId = outletId,
                DayGroup = NormalizeToken(request.DayGroup),
                request.StartTime,
                request.EndTime,
                request.AdDurationSeconds,
                request.RateZar,
                RateType = NormalizeToken(request.RateType),
                SourceName = TrimToNull(request.SourceName),
                request.SourceDate,
                request.IsActive
            },
            cancellationToken: cancellationToken));

        if (updated == 0)
        {
            throw new InvalidOperationException("Slot rate was not found.");
        }

        await UpdateOutletHasPricingFlagAsync(connection, outletId, cancellationToken);
    }

    public async Task DeleteOutletSlotRateAsync(string code, Guid slotRateId, CancellationToken cancellationToken)
    {
        var outletId = await GetOutletIdAsync(code, cancellationToken);
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        var deleted = await connection.ExecuteAsync(new CommandDefinition(
            "delete from media_outlet_slot_rate where id = @Id and media_outlet_id = @MediaOutletId;",
            new { Id = slotRateId, MediaOutletId = outletId },
            cancellationToken: cancellationToken));

        if (deleted == 0)
        {
            throw new InvalidOperationException("Slot rate was not found.");
        }

        await UpdateOutletHasPricingFlagAsync(connection, outletId, cancellationToken);
    }

    public async Task<AdminRateCardUploadResponse> UploadRateCardAsync(string channel, string? supplierOrStation, string? documentTitle, string? notes, IFormFile file, CancellationToken cancellationToken)
    {
        if (file.Length <= 0)
        {
            throw new InvalidOperationException("Choose a file to upload.");
        }

        var normalizedChannel = channel.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalizedChannel))
        {
            throw new InvalidOperationException("Channel is required.");
        }

        var uploadsDirectory = Path.Combine(_environment.ContentRootPath, "App_Data", "admin_uploads", "rate_cards");
        Directory.CreateDirectory(uploadsDirectory);

        var safeFileName = Path.GetFileName(file.FileName);
        var storedFileName = $"{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}-{safeFileName}";
        var fullPath = Path.Combine(uploadsDirectory, storedFileName);

        await using (var fileStream = File.Create(fullPath))
        {
            await file.CopyToAsync(fileStream, cancellationToken);
        }

        var importedAt = DateTime.UtcNow;
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await connection.ExecuteAsync(new CommandDefinition(
            "insert into import_manifest (source_file, channel, page_count, imported_at) values (@SourceFile, @Channel, @PageCount, @ImportedAt);",
            new
            {
                SourceFile = storedFileName,
                Channel = normalizedChannel,
                PageCount = (int?)null,
                ImportedAt = importedAt
            },
            transaction: transaction,
            cancellationToken: cancellationToken));

        await connection.ExecuteAsync(new CommandDefinition(
            "insert into package_document_metadata (source_file, channel, supplier_or_station, document_title, please_note) values (@SourceFile, @Channel, @SupplierOrStation, @DocumentTitle, @Notes);",
            new
            {
                SourceFile = storedFileName,
                Channel = normalizedChannel,
                SupplierOrStation = TrimToNull(supplierOrStation),
                DocumentTitle = TrimToNull(documentTitle) ?? safeFileName,
                Notes = TrimToNull(notes)
            },
            transaction: transaction,
            cancellationToken: cancellationToken));

        await transaction.CommitAsync(cancellationToken);

        return new AdminRateCardUploadResponse
        {
            SourceFile = storedFileName,
            StoredFileName = storedFileName,
            Channel = normalizedChannel,
            SupplierOrStation = TrimToNull(supplierOrStation),
            DocumentTitle = TrimToNull(documentTitle) ?? safeFileName,
            ImportedAt = importedAt
        };
    }

    public async Task UpdatePreviewRuleAsync(string packageCode, string tierCode, UpdateAdminPreviewRuleRequest request, CancellationToken cancellationToken)
    {
        var normalizedPackageCode = packageCode.Trim().ToLowerInvariant();
        var normalizedTierCode = tierCode.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalizedPackageCode) || string.IsNullOrWhiteSpace(normalizedTierCode))
        {
            throw new InvalidOperationException("Package code and tier code are required.");
        }

        var tierLabel = request.TierLabel.Trim();
        if (string.IsNullOrWhiteSpace(tierLabel))
        {
            throw new InvalidOperationException("Tier label is required.");
        }

        var typicalInclusionsJson = JsonSerializer.Serialize(
            request.TypicalInclusions.Select(TrimToNull).Where(static x => !string.IsNullOrWhiteSpace(x)).ToArray());
        var indicativeMixJson = JsonSerializer.Serialize(
            request.IndicativeMix.Select(TrimToNull).Where(static x => !string.IsNullOrWhiteSpace(x)).ToArray());

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var updated = await connection.ExecuteAsync(new CommandDefinition(
            @"
            update package_band_preview_tiers tier
            set
                tier_label = @TierLabel,
                typical_inclusions_json = cast(@TypicalInclusionsJson as jsonb),
                indicative_mix_json = cast(@IndicativeMixJson as jsonb),
                updated_at = now()
            from package_bands band
            where tier.package_band_id = band.id
              and lower(band.code) = @PackageCode
              and lower(tier.tier_code) = @TierCode;",
            new
            {
                TierLabel = tierLabel,
                TypicalInclusionsJson = typicalInclusionsJson,
                IndicativeMixJson = indicativeMixJson,
                PackageCode = normalizedPackageCode,
                TierCode = normalizedTierCode
            },
            cancellationToken: cancellationToken));

        if (updated == 0)
        {
            throw new InvalidOperationException("Preview rule was not found.");
        }
    }

    private static string NormalizeToken(string? value)
        => value?.Trim().ToLowerInvariant() ?? string.Empty;

    private static void ValidateOutletRequest(string code, string name)
    {
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(name))
        {
            throw new InvalidOperationException("Outlet code and name are required.");
        }
    }

    private async Task<Guid> GetOutletIdAsync(string code, CancellationToken cancellationToken)
    {
        var normalizedCode = NormalizeToken(code);
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        var outletId = await connection.ExecuteScalarAsync<Guid?>(new CommandDefinition(
            "select id from media_outlet where code = @Code;",
            new { Code = normalizedCode },
            cancellationToken: cancellationToken));

        if (!outletId.HasValue)
        {
            throw new InvalidOperationException("Outlet was not found.");
        }

        return outletId.Value;
    }

    private static async Task UpdateOutletHasPricingFlagAsync(NpgsqlConnection connection, Guid outletId, CancellationToken cancellationToken)
    {
        var hasPricing = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            @"
            select exists (
                select 1 from media_outlet_pricing_package where media_outlet_id = @MediaOutletId and is_active = true
                union all
                select 1 from media_outlet_slot_rate where media_outlet_id = @MediaOutletId and is_active = true
            );",
            new { MediaOutletId = outletId },
            cancellationToken: cancellationToken));

        await connection.ExecuteAsync(new CommandDefinition(
            "update media_outlet set has_pricing = @HasPricing, updated_at = now() where id = @Id;",
            new { Id = outletId, HasPricing = hasPricing },
            cancellationToken: cancellationToken));
    }

    private static async Task UpsertOutletAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid outletId,
        string code,
        string name,
        string mediaType,
        string coverageType,
        string catalogHealth,
        string? operatorName,
        bool isNational,
        bool hasPricing,
        string? languageNotes,
        string? targetAudience,
        string? broadcastFrequency,
        IReadOnlyList<string> primaryLanguages,
        IReadOnlyList<string> provinceCodes,
        IReadOnlyList<string> cityLabels,
        IReadOnlyList<string> audienceKeywords,
        CancellationToken cancellationToken,
        bool isUpdate)
    {
        if (isUpdate)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                @"
                update media_outlet
                set
                    code = @Code,
                    name = @Name,
                    media_type = @MediaType,
                    coverage_type = @CoverageType,
                    catalog_health = @CatalogHealth,
                    operator_name = @OperatorName,
                    is_national = @IsNational,
                    has_pricing = @HasPricing,
                    language_notes = @LanguageNotes,
                    target_audience = @TargetAudience,
                    broadcast_frequency = @BroadcastFrequency,
                    updated_at = now()
                where id = @Id;",
                new
                {
                    Id = outletId,
                    Code = code,
                    Name = name.Trim(),
                    MediaType = NormalizeToken(mediaType),
                    CoverageType = NormalizeToken(coverageType),
                    CatalogHealth = NormalizeToken(catalogHealth),
                    OperatorName = TrimToNull(operatorName),
                    IsNational = isNational,
                    HasPricing = hasPricing,
                    LanguageNotes = TrimToNull(languageNotes),
                    TargetAudience = TrimToNull(targetAudience),
                    BroadcastFrequency = TrimToNull(broadcastFrequency)
                },
                transaction: transaction,
                cancellationToken: cancellationToken));

            await connection.ExecuteAsync(new CommandDefinition("delete from media_outlet_language where media_outlet_id = @MediaOutletId;", new { MediaOutletId = outletId }, transaction: transaction, cancellationToken: cancellationToken));
            await connection.ExecuteAsync(new CommandDefinition("delete from media_outlet_geography where media_outlet_id = @MediaOutletId;", new { MediaOutletId = outletId }, transaction: transaction, cancellationToken: cancellationToken));
            await connection.ExecuteAsync(new CommandDefinition("delete from media_outlet_keyword where media_outlet_id = @MediaOutletId;", new { MediaOutletId = outletId }, transaction: transaction, cancellationToken: cancellationToken));
        }
        else
        {
            await connection.ExecuteAsync(new CommandDefinition(
                @"
                insert into media_outlet (
                    id, code, name, media_type, coverage_type, catalog_health, operator_name,
                    is_national, has_pricing, language_notes, target_audience, broadcast_frequency
                )
                values (
                    @Id, @Code, @Name, @MediaType, @CoverageType, @CatalogHealth, @OperatorName,
                    @IsNational, @HasPricing, @LanguageNotes, @TargetAudience, @BroadcastFrequency
                );",
                new
                {
                    Id = outletId,
                    Code = code,
                    Name = name.Trim(),
                    MediaType = NormalizeToken(mediaType),
                    CoverageType = NormalizeToken(coverageType),
                    CatalogHealth = NormalizeToken(catalogHealth),
                    OperatorName = TrimToNull(operatorName),
                    IsNational = isNational,
                    HasPricing = hasPricing,
                    LanguageNotes = TrimToNull(languageNotes),
                    TargetAudience = TrimToNull(targetAudience),
                    BroadcastFrequency = TrimToNull(broadcastFrequency)
                },
                transaction: transaction,
                cancellationToken: cancellationToken));
        }

        foreach (var language in primaryLanguages.Select(NormalizeToken).Where(static x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            await connection.ExecuteAsync(new CommandDefinition(
                "insert into media_outlet_language (id, media_outlet_id, language_code, is_primary) values (@Id, @MediaOutletId, @LanguageCode, @IsPrimary);",
                new { Id = Guid.NewGuid(), MediaOutletId = outletId, LanguageCode = language, IsPrimary = true },
                transaction: transaction,
                cancellationToken: cancellationToken));
        }

        foreach (var province in provinceCodes.Select(NormalizeToken).Where(static x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            await connection.ExecuteAsync(new CommandDefinition(
                "insert into media_outlet_geography (id, media_outlet_id, province_code, city_name, geography_type) values (@Id, @MediaOutletId, @ProvinceCode, null, 'province');",
                new { Id = Guid.NewGuid(), MediaOutletId = outletId, ProvinceCode = province },
                transaction: transaction,
                cancellationToken: cancellationToken));
        }

        foreach (var city in cityLabels.Select(TrimToNull).Where(static x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            await connection.ExecuteAsync(new CommandDefinition(
                "insert into media_outlet_geography (id, media_outlet_id, province_code, city_name, geography_type) values (@Id, @MediaOutletId, null, @CityName, 'city');",
                new { Id = Guid.NewGuid(), MediaOutletId = outletId, CityName = city },
                transaction: transaction,
                cancellationToken: cancellationToken));
        }

        foreach (var keyword in audienceKeywords.Select(TrimToNull).Where(static x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            await connection.ExecuteAsync(new CommandDefinition(
                "insert into media_outlet_keyword (id, media_outlet_id, keyword) values (@Id, @MediaOutletId, @Keyword);",
                new { Id = Guid.NewGuid(), MediaOutletId = outletId, Keyword = keyword },
                transaction: transaction,
                cancellationToken: cancellationToken));
        }
    }

    private static string? TrimToNull(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }

    private sealed class AdminOutletDetailRecord
    {
        public Guid Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string MediaType { get; set; } = string.Empty;
        public string CoverageType { get; set; } = string.Empty;
        public string CatalogHealth { get; set; } = string.Empty;
        public string? OperatorName { get; set; }
        public bool IsNational { get; set; }
        public bool HasPricing { get; set; }
        public string? LanguageNotes { get; set; }
        public string? TargetAudience { get; set; }
        public string? BroadcastFrequency { get; set; }
    }

    private sealed class AdminOutletGeographyRecord
    {
        public string? ProvinceCode { get; set; }
        public string? CityName { get; set; }
        public string GeographyType { get; set; } = string.Empty;
    }

    private sealed class AdminOutletPricingStatsRecord
    {
        public int PackageCount { get; set; }
        public int SlotRateCount { get; set; }
        public decimal? MinPackagePrice { get; set; }
        public decimal? MinSlotRate { get; set; }
    }
}
