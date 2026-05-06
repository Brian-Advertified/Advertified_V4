using System.Text.Json;
using Advertified.App.Contracts.Admin;
using Advertified.App.Services.Abstractions;
using Dapper;
using Npgsql;

namespace Advertified.App.Services;

public sealed class AdminMutationService : IAdminMutationService
{
    private const string AllocationBudgetBandsKey = "allocation_budget_band_rules_json";
    private const string AllocationGlobalRulesKey = "allocation_global_rules_json";
    private readonly Npgsql.NpgsqlDataSource _dataSource;
    private readonly IWebHostEnvironment _environment;
    private readonly IBroadcastInventoryCatalog _broadcastInventoryCatalog;
    private readonly IBroadcastMasterDataService _broadcastMasterDataService;

    public AdminMutationService(
        Npgsql.NpgsqlDataSource dataSource,
        IWebHostEnvironment environment,
        IBroadcastInventoryCatalog broadcastInventoryCatalog,
        IBroadcastMasterDataService broadcastMasterDataService)
    {
        _dataSource = dataSource;
        _environment = environment;
        _broadcastInventoryCatalog = broadcastInventoryCatalog;
        _broadcastMasterDataService = broadcastMasterDataService;
    }

    public async Task<AdminOutletDetailResponse> GetOutletAsync(string code, CancellationToken cancellationToken)
    {
        var normalizedCode = NormalizeToken(code);
        if (string.IsNullOrWhiteSpace(normalizedCode))
        {
            throw new InvalidOperationException("Outlet code is required.");
        }

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);

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

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);

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
        var normalizedSelection = await BuildNormalizedOutletSelectionAsync(
            request.MediaType,
            request.CoverageType,
            request.CatalogHealth,
            request.PrimaryLanguages,
            request.ProvinceCodes,
            request.CityLabels,
            request.AudienceKeywords,
            cancellationToken);

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
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
        await UpsertOutletAsync(
            connection,
            transaction,
            outletId,
            code,
            request.Name,
            request.MediaType,
            normalizedSelection.CoverageType,
            normalizedSelection.CatalogHealth,
            request.OperatorName,
            request.IsNational,
            request.HasPricing,
            request.LanguageNotes,
            request.TargetAudience,
            request.BroadcastFrequency,
            normalizedSelection.PrimaryLanguages,
            normalizedSelection.ProvinceCodes,
            normalizedSelection.CityLabels,
            normalizedSelection.AudienceKeywords,
            cancellationToken,
            isUpdate: false);
        await transaction.CommitAsync(cancellationToken);
        await _broadcastInventoryCatalog.RefreshAsync(cancellationToken);

        return new AdminOutletMutationResponse { Code = code, Name = name };
    }

    public async Task<AdminOutletMutationResponse> UpdateOutletAsync(string existingCode, UpdateAdminOutletRequest request, CancellationToken cancellationToken)
    {
        var normalizedExistingCode = NormalizeToken(existingCode);
        var nextCode = NormalizeToken(request.Code);
        var nextName = request.Name.Trim();
        ValidateOutletRequest(nextCode, nextName);
        var normalizedSelection = await BuildNormalizedOutletSelectionAsync(
            request.MediaType,
            request.CoverageType,
            request.CatalogHealth,
            request.PrimaryLanguages,
            request.ProvinceCodes,
            request.CityLabels,
            request.AudienceKeywords,
            cancellationToken);

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
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

        await UpsertOutletAsync(
            connection,
            transaction,
            outletId.Value,
            nextCode,
            request.Name,
            request.MediaType,
            normalizedSelection.CoverageType,
            normalizedSelection.CatalogHealth,
            request.OperatorName,
            request.IsNational,
            request.HasPricing,
            request.LanguageNotes,
            request.TargetAudience,
            request.BroadcastFrequency,
            normalizedSelection.PrimaryLanguages,
            normalizedSelection.ProvinceCodes,
            normalizedSelection.CityLabels,
            normalizedSelection.AudienceKeywords,
            cancellationToken,
            isUpdate: true);
        await transaction.CommitAsync(cancellationToken);
        await _broadcastInventoryCatalog.RefreshAsync(cancellationToken);

        return new AdminOutletMutationResponse { Code = nextCode, Name = nextName };
    }

    public async Task DeleteOutletAsync(string code, CancellationToken cancellationToken)
    {
        var normalizedCode = NormalizeToken(code);
        if (string.IsNullOrWhiteSpace(normalizedCode))
        {
            throw new InvalidOperationException("Outlet code is required.");
        }

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);

        var deleted = await connection.ExecuteAsync(new CommandDefinition(
            "delete from media_outlet where code = @Code;",
            new { Code = normalizedCode },
            cancellationToken: cancellationToken));

        if (deleted == 0)
        {
            throw new InvalidOperationException("Outlet was not found.");
        }

        await _broadcastInventoryCatalog.RefreshAsync(cancellationToken);
    }

    public async Task<Guid> CreateOutletPricingPackageAsync(string code, UpsertAdminOutletPricingPackageRequest request, CancellationToken cancellationToken)
    {
        var outletId = await GetOutletIdAsync(code, cancellationToken);
        var packageId = Guid.NewGuid();

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
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
        await _broadcastInventoryCatalog.RefreshAsync(cancellationToken);
        return packageId;
    }

    public async Task UpdateOutletPricingPackageAsync(string code, Guid packageId, UpsertAdminOutletPricingPackageRequest request, CancellationToken cancellationToken)
    {
        var outletId = await GetOutletIdAsync(code, cancellationToken);

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
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
        await _broadcastInventoryCatalog.RefreshAsync(cancellationToken);
    }

    public async Task DeleteOutletPricingPackageAsync(string code, Guid packageId, CancellationToken cancellationToken)
    {
        var outletId = await GetOutletIdAsync(code, cancellationToken);
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        var deleted = await connection.ExecuteAsync(new CommandDefinition(
            "delete from media_outlet_pricing_package where id = @Id and media_outlet_id = @MediaOutletId;",
            new { Id = packageId, MediaOutletId = outletId },
            cancellationToken: cancellationToken));

        if (deleted == 0)
        {
            throw new InvalidOperationException("Pricing package was not found.");
        }

        await UpdateOutletHasPricingFlagAsync(connection, outletId, cancellationToken);
        await _broadcastInventoryCatalog.RefreshAsync(cancellationToken);
    }

    public async Task<Guid> CreateOutletSlotRateAsync(string code, UpsertAdminOutletSlotRateRequest request, CancellationToken cancellationToken)
    {
        var outletId = await GetOutletIdAsync(code, cancellationToken);
        var slotRateId = Guid.NewGuid();
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
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
        await _broadcastInventoryCatalog.RefreshAsync(cancellationToken);
        return slotRateId;
    }

    public async Task UpdateOutletSlotRateAsync(string code, Guid slotRateId, UpsertAdminOutletSlotRateRequest request, CancellationToken cancellationToken)
    {
        var outletId = await GetOutletIdAsync(code, cancellationToken);
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
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
        await _broadcastInventoryCatalog.RefreshAsync(cancellationToken);
    }

    public async Task DeleteOutletSlotRateAsync(string code, Guid slotRateId, CancellationToken cancellationToken)
    {
        var outletId = await GetOutletIdAsync(code, cancellationToken);
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        var deleted = await connection.ExecuteAsync(new CommandDefinition(
            "delete from media_outlet_slot_rate where id = @Id and media_outlet_id = @MediaOutletId;",
            new { Id = slotRateId, MediaOutletId = outletId },
            cancellationToken: cancellationToken));

        if (deleted == 0)
        {
            throw new InvalidOperationException("Slot rate was not found.");
        }

        await UpdateOutletHasPricingFlagAsync(connection, outletId, cancellationToken);
        await _broadcastInventoryCatalog.RefreshAsync(cancellationToken);
    }

    public async Task<AdminGeographyDetailResponse> GetGeographyAsync(string code, CancellationToken cancellationToken)
    {
        var normalizedCode = NormalizeToken(code);
        if (string.IsNullOrWhiteSpace(normalizedCode))
        {
            throw new InvalidOperationException("Area code is required.");
        }

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);

        var area = await connection.QuerySingleOrDefaultAsync<AdminGeographyRecord>(new CommandDefinition(
            @"
            select
                pap.id,
                pap.cluster_code as Code,
                pap.display_name as Label,
                coalesce(pap.description, '') as Description,
                pap.fallback_locations_json as FallbackLocationsJson,
                pap.sort_order as SortOrder,
                pap.is_active as IsActive,
                rc.id as ClusterId
            from package_area_profiles pap
            left join region_clusters rc on rc.code = pap.cluster_code
            where lower(pap.cluster_code) = @Code;",
            new { Code = normalizedCode },
            cancellationToken: cancellationToken));

        if (area is null)
        {
            throw new InvalidOperationException("Area mapping was not found.");
        }

        var mappings = area.ClusterId.HasValue
            ? (await connection.QueryAsync<AdminGeographyMappingResponse>(new CommandDefinition(
                @"
                select
                    id,
                    province as Province,
                    city as City,
                    station_or_channel_name as StationOrChannelName
                from region_cluster_mappings
                where cluster_id = @ClusterId
                order by province nulls first, city nulls first, station_or_channel_name nulls first;",
                new { ClusterId = area.ClusterId.Value },
                cancellationToken: cancellationToken))).ToArray()
            : Array.Empty<AdminGeographyMappingResponse>();

        return new AdminGeographyDetailResponse
        {
            Id = area.Id,
            Code = area.Code,
            Label = area.Label,
            Description = area.Description,
            FallbackLocations = DeserializeJsonArray(area.FallbackLocationsJson),
            SortOrder = area.SortOrder,
            IsActive = area.IsActive,
            Mappings = mappings,
        };
    }

    public async Task<AdminGeographyDetailResponse> CreateGeographyAsync(CreateAdminGeographyRequest request, CancellationToken cancellationToken)
    {
        var code = NormalizeToken(request.Code);
        var label = TrimToNull(request.Label);
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(label))
        {
            throw new InvalidOperationException("Area code and label are required.");
        }

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var clusterExists = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "select count(*) from region_clusters where code = @Code;",
            new { Code = code },
            transaction: transaction,
            cancellationToken: cancellationToken));

        if (clusterExists > 0)
        {
            throw new InvalidOperationException("An area with this code already exists.");
        }

        var clusterId = Guid.NewGuid();
        await connection.ExecuteAsync(new CommandDefinition(
            @"
            insert into region_clusters (id, code, name, description)
            values (@Id, @Code, @Name, @Description);",
            new
            {
                Id = clusterId,
                Code = code,
                Name = label,
                Description = TrimToNull(request.Description),
            },
            transaction: transaction,
            cancellationToken: cancellationToken));

        await connection.ExecuteAsync(new CommandDefinition(
            @"
            insert into package_area_profiles (id, cluster_code, display_name, description, fallback_locations_json, sort_order, is_active)
            values (@Id, @Code, @Label, @Description, cast(@FallbackLocationsJson as jsonb), @SortOrder, @IsActive);",
            new
            {
                Id = Guid.NewGuid(),
                Code = code,
                Label = label,
                Description = TrimToNull(request.Description),
                FallbackLocationsJson = SerializeJsonArray(request.FallbackLocations),
                request.SortOrder,
                request.IsActive,
            },
            transaction: transaction,
            cancellationToken: cancellationToken));

        await transaction.CommitAsync(cancellationToken);
        return await GetGeographyAsync(code, cancellationToken);
    }

    public async Task<AdminGeographyDetailResponse> UpdateGeographyAsync(string existingCode, UpdateAdminGeographyRequest request, CancellationToken cancellationToken)
    {
        var normalizedExistingCode = NormalizeToken(existingCode);
        var nextCode = NormalizeToken(request.Code);
        var nextLabel = TrimToNull(request.Label);
        if (string.IsNullOrWhiteSpace(normalizedExistingCode) || string.IsNullOrWhiteSpace(nextCode) || string.IsNullOrWhiteSpace(nextLabel))
        {
            throw new InvalidOperationException("Area code and label are required.");
        }

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var cluster = await connection.QuerySingleOrDefaultAsync<RegionClusterLookupRecord>(new CommandDefinition(
            "select id, code from region_clusters where code = @Code;",
            new { Code = normalizedExistingCode },
            transaction: transaction,
            cancellationToken: cancellationToken));

        if (cluster is null)
        {
            throw new InvalidOperationException("Area mapping was not found.");
        }

        if (!string.Equals(normalizedExistingCode, nextCode, StringComparison.OrdinalIgnoreCase))
        {
            var duplicateExists = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                "select count(*) from region_clusters where code = @Code and id <> @Id;",
                new { Code = nextCode, Id = cluster.Id },
                transaction: transaction,
                cancellationToken: cancellationToken));

            if (duplicateExists > 0)
            {
                throw new InvalidOperationException("Another area with this code already exists.");
            }
        }

        await connection.ExecuteAsync(new CommandDefinition(
            @"
            update region_clusters
            set
                code = @NextCode,
                name = @Name,
                description = @Description,
                updated_at = now()
            where id = @Id;",
            new
            {
                Id = cluster.Id,
                NextCode = nextCode,
                Name = nextLabel,
                Description = TrimToNull(request.Description),
            },
            transaction: transaction,
            cancellationToken: cancellationToken));

        await connection.ExecuteAsync(new CommandDefinition(
            @"
            update package_area_profiles
            set
                cluster_code = @NextCode,
                display_name = @Label,
                description = @Description,
                fallback_locations_json = cast(@FallbackLocationsJson as jsonb),
                sort_order = @SortOrder,
                is_active = @IsActive,
                updated_at = now()
            where cluster_code = @ExistingCode;",
            new
            {
                ExistingCode = normalizedExistingCode,
                NextCode = nextCode,
                Label = nextLabel,
                Description = TrimToNull(request.Description),
                FallbackLocationsJson = SerializeJsonArray(request.FallbackLocations),
                request.SortOrder,
                request.IsActive,
            },
            transaction: transaction,
            cancellationToken: cancellationToken));

        await transaction.CommitAsync(cancellationToken);
        return await GetGeographyAsync(nextCode, cancellationToken);
    }

    public async Task DeleteGeographyAsync(string code, CancellationToken cancellationToken)
    {
        var normalizedCode = NormalizeToken(code);
        if (string.IsNullOrWhiteSpace(normalizedCode))
        {
            throw new InvalidOperationException("Area code is required.");
        }

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);

        var deleted = await connection.ExecuteAsync(new CommandDefinition(
            "delete from region_clusters where code = @Code;",
            new { Code = normalizedCode },
            cancellationToken: cancellationToken));

        if (deleted == 0)
        {
            throw new InvalidOperationException("Area mapping was not found.");
        }
    }

    public async Task<Guid> CreateGeographyMappingAsync(string code, UpsertAdminGeographyMappingRequest request, CancellationToken cancellationToken)
    {
        var clusterId = await GetRegionClusterIdAsync(code, cancellationToken);
        ValidateGeographyMappingRequest(request);
        var mappingId = Guid.NewGuid();

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            @"
            insert into region_cluster_mappings (id, cluster_id, province, city, station_or_channel_name)
            values (@Id, @ClusterId, @Province, @City, @StationOrChannelName);",
            new
            {
                Id = mappingId,
                ClusterId = clusterId,
                Province = NormalizeProvinceCode(request.Province),
                City = TrimToNull(request.City),
                StationOrChannelName = TrimToNull(request.StationOrChannelName),
            },
            cancellationToken: cancellationToken));

        return mappingId;
    }

    public async Task UpdateGeographyMappingAsync(string code, Guid mappingId, UpsertAdminGeographyMappingRequest request, CancellationToken cancellationToken)
    {
        var clusterId = await GetRegionClusterIdAsync(code, cancellationToken);
        ValidateGeographyMappingRequest(request);

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        var updated = await connection.ExecuteAsync(new CommandDefinition(
            @"
            update region_cluster_mappings
            set
                province = @Province,
                city = @City,
                station_or_channel_name = @StationOrChannelName,
                updated_at = now()
            where id = @Id and cluster_id = @ClusterId;",
            new
            {
                Id = mappingId,
                ClusterId = clusterId,
                Province = NormalizeProvinceCode(request.Province),
                City = TrimToNull(request.City),
                StationOrChannelName = TrimToNull(request.StationOrChannelName),
            },
            cancellationToken: cancellationToken));

        if (updated == 0)
        {
            throw new InvalidOperationException("Geography mapping was not found.");
        }
    }

    public async Task DeleteGeographyMappingAsync(string code, Guid mappingId, CancellationToken cancellationToken)
    {
        var clusterId = await GetRegionClusterIdAsync(code, cancellationToken);
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        var deleted = await connection.ExecuteAsync(new CommandDefinition(
            "delete from region_cluster_mappings where id = @Id and cluster_id = @ClusterId;",
            new { Id = mappingId, ClusterId = clusterId },
            cancellationToken: cancellationToken));

        if (deleted == 0)
        {
            throw new InvalidOperationException("Geography mapping was not found.");
        }
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
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
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
        await _broadcastInventoryCatalog.RefreshAsync(cancellationToken);

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

    public async Task UpdateRateCardAsync(string sourceFile, UpdateAdminRateCardRequest request, CancellationToken cancellationToken)
    {
        var normalizedSourceFile = TrimToNull(sourceFile);
        if (normalizedSourceFile is null)
        {
            throw new InvalidOperationException("Source file is required.");
        }

        var normalizedChannel = NormalizeToken(request.Channel);
        if (string.IsNullOrWhiteSpace(normalizedChannel))
        {
            throw new InvalidOperationException("Channel is required.");
        }

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var importUpdated = await connection.ExecuteAsync(new CommandDefinition(
            @"
            update import_manifest
            set channel = @Channel
            where source_file = @SourceFile;",
            new
            {
                SourceFile = normalizedSourceFile,
                Channel = normalizedChannel,
            },
            transaction: transaction,
            cancellationToken: cancellationToken));

        if (importUpdated == 0)
        {
            throw new InvalidOperationException("Rate card import was not found.");
        }

        var metadataUpdated = await connection.ExecuteAsync(new CommandDefinition(
            @"
            update package_document_metadata
            set
                channel = @Channel,
                supplier_or_station = @SupplierOrStation,
                document_title = @DocumentTitle,
                please_note = @Notes
            where source_file = @SourceFile;",
            new
            {
                SourceFile = normalizedSourceFile,
                Channel = normalizedChannel,
                SupplierOrStation = TrimToNull(request.SupplierOrStation),
                DocumentTitle = TrimToNull(request.DocumentTitle),
                Notes = TrimToNull(request.Notes),
            },
            transaction: transaction,
            cancellationToken: cancellationToken));

        if (metadataUpdated == 0)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                @"
                insert into package_document_metadata (source_file, channel, supplier_or_station, document_title, please_note)
                values (@SourceFile, @Channel, @SupplierOrStation, @DocumentTitle, @Notes);",
                new
                {
                    SourceFile = normalizedSourceFile,
                    Channel = normalizedChannel,
                    SupplierOrStation = TrimToNull(request.SupplierOrStation),
                    DocumentTitle = TrimToNull(request.DocumentTitle) ?? normalizedSourceFile,
                    Notes = TrimToNull(request.Notes),
                },
                transaction: transaction,
                cancellationToken: cancellationToken));
        }

        await transaction.CommitAsync(cancellationToken);
        await _broadcastInventoryCatalog.RefreshAsync(cancellationToken);
    }

    public async Task DeleteRateCardAsync(string sourceFile, CancellationToken cancellationToken)
    {
        var normalizedSourceFile = TrimToNull(sourceFile);
        if (normalizedSourceFile is null)
        {
            throw new InvalidOperationException("Source file is required.");
        }

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await connection.ExecuteAsync(new CommandDefinition(
            "delete from package_document_metadata where source_file = @SourceFile;",
            new { SourceFile = normalizedSourceFile },
            transaction: transaction,
            cancellationToken: cancellationToken));

        var deleted = await connection.ExecuteAsync(new CommandDefinition(
            "delete from import_manifest where source_file = @SourceFile;",
            new { SourceFile = normalizedSourceFile },
            transaction: transaction,
            cancellationToken: cancellationToken));

        if (deleted == 0)
        {
            throw new InvalidOperationException("Rate card import was not found.");
        }

        await transaction.CommitAsync(cancellationToken);
        await _broadcastInventoryCatalog.RefreshAsync(cancellationToken);

        var fullPath = Path.Combine(_environment.ContentRootPath, "App_Data", "admin_uploads", "rate_cards", normalizedSourceFile);
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }
    }

    public async Task<Guid> CreatePackageSettingAsync(CreateAdminPackageSettingRequest request, CancellationToken cancellationToken)
    {
        ValidatePackageSettingRequest(request.Code, request.Name, request.MinBudget, request.MaxBudget, request.LeadTime);

        var packageBandId = Guid.NewGuid();
        var normalizedCode = NormalizeToken(request.Code);
        var benefitsJson = SerializeJsonArray(request.Benefits);

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var existing = await connection.ExecuteScalarAsync<Guid?>(new CommandDefinition(
            "select id from package_bands where lower(code) = @Code;",
            new { Code = normalizedCode },
            transaction: transaction,
            cancellationToken: cancellationToken));

        if (existing.HasValue)
        {
            throw new InvalidOperationException("A package band with this code already exists.");
        }

        await connection.ExecuteAsync(new CommandDefinition(
            @"
            insert into package_bands (id, code, name, min_budget, max_budget, sort_order, is_active)
            values (@Id, @Code, @Name, @MinBudget, @MaxBudget, @SortOrder, @IsActive);",
            new
            {
                Id = packageBandId,
                Code = normalizedCode,
                Name = request.Name.Trim(),
                request.MinBudget,
                request.MaxBudget,
                request.SortOrder,
                request.IsActive
            },
            transaction: transaction,
            cancellationToken: cancellationToken));

        await UpsertPackageBandProfileAsync(connection, transaction, packageBandId, request.Description, request.AudienceFit, request.QuickBenefit, request.PackagePurpose, request.IncludeRadio, request.IncludeTv, request.LeadTime, request.RecommendedSpend, request.IsRecommended, benefitsJson, cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return packageBandId;
    }

    public async Task UpdatePackageSettingAsync(Guid packageSettingId, UpdateAdminPackageSettingRequest request, CancellationToken cancellationToken)
    {
        ValidatePackageSettingRequest(request.Code, request.Name, request.MinBudget, request.MaxBudget, request.LeadTime);

        var normalizedCode = NormalizeToken(request.Code);
        var benefitsJson = SerializeJsonArray(request.Benefits);

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var existing = await connection.ExecuteScalarAsync<Guid?>(new CommandDefinition(
            "select id from package_bands where id = @Id;",
            new { Id = packageSettingId },
            transaction: transaction,
            cancellationToken: cancellationToken));

        if (!existing.HasValue)
        {
            throw new InvalidOperationException("Package setting was not found.");
        }

        var duplicate = await connection.ExecuteScalarAsync<Guid?>(new CommandDefinition(
            "select id from package_bands where lower(code) = @Code and id <> @Id;",
            new { Id = packageSettingId, Code = normalizedCode },
            transaction: transaction,
            cancellationToken: cancellationToken));

        if (duplicate.HasValue)
        {
            throw new InvalidOperationException("A package band with this code already exists.");
        }

        await connection.ExecuteAsync(new CommandDefinition(
            @"
            update package_bands
            set
                code = @Code,
                name = @Name,
                min_budget = @MinBudget,
                max_budget = @MaxBudget,
                sort_order = @SortOrder,
                is_active = @IsActive
            where id = @Id;",
            new
            {
                Id = packageSettingId,
                Code = normalizedCode,
                Name = request.Name.Trim(),
                request.MinBudget,
                request.MaxBudget,
                request.SortOrder,
                request.IsActive
            },
            transaction: transaction,
            cancellationToken: cancellationToken));

        await UpsertPackageBandProfileAsync(connection, transaction, packageSettingId, request.Description, request.AudienceFit, request.QuickBenefit, request.PackagePurpose, request.IncludeRadio, request.IncludeTv, request.LeadTime, request.RecommendedSpend, request.IsRecommended, benefitsJson, cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task DeletePackageSettingAsync(Guid packageSettingId, CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var exists = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            "select exists(select 1 from package_bands where id = @Id);",
            new { Id = packageSettingId },
            transaction: transaction,
            cancellationToken: cancellationToken));

        if (!exists)
        {
            throw new InvalidOperationException("Package setting was not found.");
        }

        var hasDependencies = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            @"
            select exists(select 1 from campaigns where package_band_id = @Id)
                or exists(select 1 from package_orders where package_band_id = @Id);",
            new { Id = packageSettingId },
            transaction: transaction,
            cancellationToken: cancellationToken));

        if (hasDependencies)
        {
            throw new InvalidOperationException("This package band already has linked campaigns or orders and cannot be deleted. Set it inactive instead.");
        }

        await connection.ExecuteAsync(new CommandDefinition(
            "delete from package_bands where id = @Id;",
            new { Id = packageSettingId },
            transaction: transaction,
            cancellationToken: cancellationToken));

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task UpdateEnginePolicyAsync(string packageCode, UpdateAdminEnginePolicyRequest request, CancellationToken cancellationToken)
    {
        var normalizedPackageCode = NormalizeToken(packageCode);
        if (string.IsNullOrWhiteSpace(normalizedPackageCode))
        {
            throw new InvalidOperationException("Package code is required.");
        }

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            @"
            insert into admin_engine_policy_overrides (
                package_code, budget_floor, minimum_national_radio_candidates,
                require_national_capable_radio, require_premium_national_radio,
                national_radio_bonus, non_national_radio_penalty, regional_radio_penalty
            ) values (
                @PackageCode, @BudgetFloor, @MinimumNationalRadioCandidates,
                @RequireNationalCapableRadio, @RequirePremiumNationalRadio,
                @NationalRadioBonus, @NonNationalRadioPenalty, @RegionalRadioPenalty
            )
            on conflict (package_code) do update
            set
                budget_floor = excluded.budget_floor,
                minimum_national_radio_candidates = excluded.minimum_national_radio_candidates,
                require_national_capable_radio = excluded.require_national_capable_radio,
                require_premium_national_radio = excluded.require_premium_national_radio,
                national_radio_bonus = excluded.national_radio_bonus,
                non_national_radio_penalty = excluded.non_national_radio_penalty,
                regional_radio_penalty = excluded.regional_radio_penalty,
                updated_at = now();",
            new
            {
                PackageCode = normalizedPackageCode,
                request.BudgetFloor,
                request.MinimumNationalRadioCandidates,
                request.RequireNationalCapableRadio,
                request.RequirePremiumNationalRadio,
                request.NationalRadioBonus,
                request.NonNationalRadioPenalty,
                request.RegionalRadioPenalty,
            },
            cancellationToken: cancellationToken));
    }

    public async Task UpdatePlanningAllocationSettingsAsync(UpdateAdminPlanningAllocationSettingsRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidatePlanningAllocationSettingsRequest(request);

        var budgetBandsJson = JsonSerializer.Serialize(
            request.BudgetBands
                .OrderBy(band => band.Min)
                .ThenBy(band => band.Max)
                .Select(band => new
                {
                    name = band.Name.Trim(),
                    min = band.Min,
                    max = band.Max,
                    oohTarget = band.OohTarget,
                    billboardShareOfOoh = band.BillboardShareOfOoh,
                    tvMin = band.TvMin,
                    tvEligible = band.TvEligible,
                    radioRange = band.RadioRange,
                    digitalRange = band.DigitalRange
                })
                .ToArray());
        var globalRulesJson = JsonSerializer.Serialize(new
        {
            maxOoh = request.GlobalRules.MaxOoh,
            minDigital = request.GlobalRules.MinDigital,
            enforceTvFloorIfPreferred = request.GlobalRules.EnforceTvFloorIfPreferred
        });

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            @"
            insert into planning_engine_settings (setting_key, setting_value, description)
            values
                (@BudgetBandsKey, @BudgetBandsJson, 'Planning allocation budget bands. Operators can tune Billboard, Digital Screen, TV, radio, and digital distribution by budget band without a deployment.'),
                (@GlobalRulesKey, @GlobalRulesJson, 'Planning allocation global rules. Operators can cap billboard and digital screen share together, set a digital floor, and require a TV floor when TV is preferred.')
            on conflict (setting_key) do update
            set
                setting_value = excluded.setting_value,
                description = excluded.description,
                updated_at = now();",
            new
            {
                BudgetBandsKey = AllocationBudgetBandsKey,
                BudgetBandsJson = budgetBandsJson,
                GlobalRulesKey = AllocationGlobalRulesKey,
                GlobalRulesJson = globalRulesJson
            },
            cancellationToken: cancellationToken));
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

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);

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

    public async Task UpdatePricingSettingsAsync(UpdateAdminPricingSettingsRequest request, CancellationToken cancellationToken)
    {
        ValidatePercentage(request.AiStudioReservePercent, nameof(request.AiStudioReservePercent));
        ValidatePercentage(request.OohMarkupPercent, nameof(request.OohMarkupPercent));
        ValidatePercentage(request.RadioMarkupPercent, nameof(request.RadioMarkupPercent));
        ValidatePercentage(request.TvMarkupPercent, nameof(request.TvMarkupPercent));
        ValidatePercentage(request.NewspaperMarkupPercent, nameof(request.NewspaperMarkupPercent));
        ValidatePercentage(request.DigitalMarkupPercent, nameof(request.DigitalMarkupPercent));
        ValidatePercentage(request.SalesCommissionPercent, nameof(request.SalesCommissionPercent));
        ValidatePercentage(request.SalesAgentShareBelowThresholdPercent, nameof(request.SalesAgentShareBelowThresholdPercent));
        ValidatePercentage(request.SalesAgentShareAtOrAboveThresholdPercent, nameof(request.SalesAgentShareAtOrAboveThresholdPercent));
        if (request.SalesCommissionThresholdZar < 0m)
        {
            throw new InvalidOperationException("Sales commission threshold must be zero or greater.");
        }

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            @"
            insert into pricing_settings (
                pricing_key,
                ai_studio_reserve_percent,
                ooh_markup_percent,
                radio_markup_percent,
                tv_markup_percent,
                newspaper_markup_percent,
                digital_markup_percent,
                sales_commission_percent,
                sales_commission_threshold_zar,
                sales_agent_share_below_threshold_percent,
                sales_agent_share_at_or_above_threshold_percent
            ) values (
                'default',
                @AiStudioReservePercent,
                @OohMarkupPercent,
                @RadioMarkupPercent,
                @TvMarkupPercent,
                @NewspaperMarkupPercent,
                @DigitalMarkupPercent,
                @SalesCommissionPercent,
                @SalesCommissionThresholdZar,
                @SalesAgentShareBelowThresholdPercent,
                @SalesAgentShareAtOrAboveThresholdPercent
            )
            on conflict (pricing_key) do update
            set
                ai_studio_reserve_percent = excluded.ai_studio_reserve_percent,
                ooh_markup_percent = excluded.ooh_markup_percent,
                radio_markup_percent = excluded.radio_markup_percent,
                tv_markup_percent = excluded.tv_markup_percent,
                newspaper_markup_percent = excluded.newspaper_markup_percent,
                digital_markup_percent = excluded.digital_markup_percent,
                sales_commission_percent = excluded.sales_commission_percent,
                sales_commission_threshold_zar = excluded.sales_commission_threshold_zar,
                sales_agent_share_below_threshold_percent = excluded.sales_agent_share_below_threshold_percent,
                sales_agent_share_at_or_above_threshold_percent = excluded.sales_agent_share_at_or_above_threshold_percent,
                updated_at = now();",
            new
            {
                request.AiStudioReservePercent,
                request.OohMarkupPercent,
                request.RadioMarkupPercent,
                request.TvMarkupPercent,
                request.NewspaperMarkupPercent,
                request.DigitalMarkupPercent,
                request.SalesCommissionPercent,
                request.SalesCommissionThresholdZar,
                request.SalesAgentShareBelowThresholdPercent,
                request.SalesAgentShareAtOrAboveThresholdPercent
            },
            cancellationToken: cancellationToken));
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
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
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

    private async Task<Guid> GetRegionClusterIdAsync(string code, CancellationToken cancellationToken)
    {
        var normalizedCode = NormalizeToken(code);
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        var clusterId = await connection.ExecuteScalarAsync<Guid?>(new CommandDefinition(
            "select id from region_clusters where code = @Code;",
            new { Code = normalizedCode },
            cancellationToken: cancellationToken));

        if (!clusterId.HasValue)
        {
            throw new InvalidOperationException("Area mapping was not found.");
        }

        return clusterId.Value;
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

    private async Task UpsertOutletAsync(
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
                    preserve_imported_core_metadata = true,
                    preserve_imported_languages = true,
                    preserve_imported_geography = true,
                    preserve_imported_keywords = true,
                    updated_at = now()
                where id = @Id;",
                new
                {
                    Id = outletId,
                    Code = code,
                    Name = name.Trim(),
                    MediaType = NormalizeToken(mediaType),
                    CoverageType = coverageType,
                    CatalogHealth = catalogHealth,
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
                    is_national, has_pricing, language_notes, target_audience, broadcast_frequency,
                    preserve_imported_core_metadata, preserve_imported_languages, preserve_imported_geography, preserve_imported_keywords
                )
                values (
                    @Id, @Code, @Name, @MediaType, @CoverageType, @CatalogHealth, @OperatorName,
                    @IsNational, @HasPricing, @LanguageNotes, @TargetAudience, @BroadcastFrequency,
                    true, true, true, true
                );",
                new
                {
                    Id = outletId,
                    Code = code,
                    Name = name.Trim(),
                    MediaType = NormalizeToken(mediaType),
                    CoverageType = coverageType,
                    CatalogHealth = catalogHealth,
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

        foreach (var language in primaryLanguages
            .Select(_broadcastMasterDataService.NormalizeLanguageCode)
            .Where(static x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            await connection.ExecuteAsync(new CommandDefinition(
                "insert into media_outlet_language (id, media_outlet_id, language_code, is_primary) values (@Id, @MediaOutletId, @LanguageCode, @IsPrimary);",
                new { Id = Guid.NewGuid(), MediaOutletId = outletId, LanguageCode = language, IsPrimary = true },
                transaction: transaction,
                cancellationToken: cancellationToken));
        }

        foreach (var province in provinceCodes
            .Select(_broadcastMasterDataService.NormalizeProvinceCode)
            .Where(static x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase))
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

    private string? NormalizeProvinceCode(string? value)
    {
        var trimmed = TrimToNull(value);
        if (trimmed is null)
        {
            return null;
        }

        return _broadcastMasterDataService.NormalizeProvinceCode(trimmed);
    }

    private async Task<NormalizedOutletSelection> BuildNormalizedOutletSelectionAsync(
        string mediaType,
        string coverageType,
        string catalogHealth,
        IReadOnlyList<string> primaryLanguages,
        IReadOnlyList<string> provinceCodes,
        IReadOnlyList<string> cityLabels,
        IReadOnlyList<string> audienceKeywords,
        CancellationToken cancellationToken)
    {
        var masterData = await _broadcastMasterDataService.GetOutletMasterDataAsync(cancellationToken);

        var allowedLanguages = masterData.Languages
            .Select(option => option.Value.Trim().ToLowerInvariant())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var allowedProvinces = masterData.Provinces
            .Select(option => option.Value.Trim().ToLowerInvariant())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var allowedCoverageTypes = masterData.CoverageTypes
            .Select(option => option.Value.Trim().ToLowerInvariant())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var allowedCatalogHealthStates = masterData.CatalogHealthStates
            .Select(option => option.Value.Trim().ToLowerInvariant())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var cityLookup = BuildLabelLookup(masterData.Cities);
        var keywordLookup = BuildLabelLookup(masterData.AudienceKeywords);
        var normalizedMediaType = NormalizeToken(mediaType);
        var isBroadcastOutlet = normalizedMediaType is "radio" or "tv";

        var normalizedCoverageType = _broadcastMasterDataService.NormalizeCoverageType(coverageType);
        if (isBroadcastOutlet && !allowedCoverageTypes.Contains(normalizedCoverageType))
        {
            throw new InvalidOperationException($"Invalid coverage type '{coverageType}'. Choose a canonical coverage option.");
        }

        var normalizedCatalogHealth = _broadcastMasterDataService.NormalizeCatalogHealth(catalogHealth);
        if (isBroadcastOutlet && !allowedCatalogHealthStates.Contains(normalizedCatalogHealth))
        {
            throw new InvalidOperationException($"Invalid catalog health '{catalogHealth}'. Choose a canonical health state.");
        }

        var normalizedPrimaryLanguages = primaryLanguages
            .Select(_broadcastMasterDataService.NormalizeLanguageCode)
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (isBroadcastOutlet)
        {
            var invalidLanguages = normalizedPrimaryLanguages
                .Where(value => !allowedLanguages.Contains(value))
                .ToArray();
            if (invalidLanguages.Length > 0)
            {
                throw new InvalidOperationException(
                    $"Invalid language selection: {string.Join(", ", invalidLanguages.Take(3))}. Use master-data language options only.");
            }
        }

        var normalizedProvinceCodes = provinceCodes
            .Select(_broadcastMasterDataService.NormalizeProvinceCode)
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (isBroadcastOutlet)
        {
            var invalidProvinces = normalizedProvinceCodes
                .Where(value => !allowedProvinces.Contains(value))
                .ToArray();
            if (invalidProvinces.Length > 0)
            {
                throw new InvalidOperationException(
                    $"Invalid province selection: {string.Join(", ", invalidProvinces.Take(3))}. Use master-data province options only.");
            }
        }

        var normalizedCities = cityLabels
            .Select(TrimToNull)
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(value => cityLookup.TryGetValue(value!, out var canonical) ? canonical : value!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (isBroadcastOutlet)
        {
            var invalidCities = normalizedCities
                .Where(city => !cityLookup.ContainsKey(city))
                .ToArray();
            if (invalidCities.Length > 0)
            {
                throw new InvalidOperationException(
                    $"Invalid city selection: {string.Join(", ", invalidCities.Take(3))}. Use canonical city options only.");
            }
        }

        var normalizedAudienceKeywords = audienceKeywords
            .Select(TrimToNull)
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(value => keywordLookup.TryGetValue(value!, out var canonical) ? canonical : value!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (isBroadcastOutlet)
        {
            var invalidKeywords = normalizedAudienceKeywords
                .Where(keyword => !keywordLookup.ContainsKey(keyword))
                .ToArray();
            if (invalidKeywords.Length > 0)
            {
                throw new InvalidOperationException(
                    $"Invalid audience keyword selection: {string.Join(", ", invalidKeywords.Take(3))}. Use canonical audience keyword options only.");
            }
        }

        return new NormalizedOutletSelection(
            normalizedCoverageType,
            normalizedCatalogHealth,
            normalizedPrimaryLanguages,
            normalizedProvinceCodes,
            normalizedCities,
            normalizedAudienceKeywords);
    }

    private static Dictionary<string, string> BuildLabelLookup(IReadOnlyList<AdminLookupOptionResponse> options)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var option in options)
        {
            if (string.IsNullOrWhiteSpace(option.Label))
            {
                continue;
            }

            var canonical = option.Label.Trim();
            map[canonical] = canonical;
            map[canonical.ToLowerInvariant()] = canonical;
            map[canonical.Replace("-", " ", StringComparison.Ordinal).ToLowerInvariant()] = canonical;
            map[canonical.Replace(" ", string.Empty, StringComparison.Ordinal).ToLowerInvariant()] = canonical;
            if (!string.IsNullOrWhiteSpace(option.Value))
            {
                map[option.Value.Trim()] = canonical;
            }
        }

        return map;
    }

    private static string SerializeJsonArray(IEnumerable<string> values)
    {
        return JsonSerializer.Serialize(values
            .Select(TrimToNull)
            .Where(static x => !string.IsNullOrWhiteSpace(x))
            .ToArray());
    }

    private static string[] DeserializeJsonArray(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Array.Empty<string>();
        }

        try
        {
            return JsonSerializer.Deserialize<string[]>(json) ?? Array.Empty<string>();
        }
        catch (JsonException)
        {
            return Array.Empty<string>();
        }
    }

    private static void ValidateGeographyMappingRequest(UpsertAdminGeographyMappingRequest request)
    {
        var hasValue = !string.IsNullOrWhiteSpace(request.Province)
            || !string.IsNullOrWhiteSpace(request.City)
            || !string.IsNullOrWhiteSpace(request.StationOrChannelName);
        if (!hasValue)
        {
            throw new InvalidOperationException("At least one mapping field is required.");
        }
    }

    private static void ValidatePackageSettingRequest(string code, string name, decimal minBudget, decimal maxBudget, string leadTime)
    {
        if (string.IsNullOrWhiteSpace(NormalizeToken(code)) || string.IsNullOrWhiteSpace(TrimToNull(name)))
        {
            throw new InvalidOperationException("Package code and name are required.");
        }

        if (minBudget < 0 || maxBudget <= 0 || maxBudget < minBudget)
        {
            throw new InvalidOperationException("Package budgets must be valid and max budget must be greater than or equal to min budget.");
        }

        if (string.IsNullOrWhiteSpace(TrimToNull(leadTime)))
        {
            throw new InvalidOperationException("Lead time is required.");
        }
    }

    private static void ValidatePercentage(decimal value, string label)
    {
        if (value < 0m || value > 1m)
        {
            throw new InvalidOperationException($"{label} must be between 0 and 1.");
        }
    }

    private static void ValidatePlanningAllocationSettingsRequest(UpdateAdminPlanningAllocationSettingsRequest request)
    {
        if (request.BudgetBands.Count == 0)
        {
            throw new InvalidOperationException("At least one planning allocation budget band is required.");
        }

        ValidatePercentage(request.GlobalRules.MaxOoh, "Max billboards and digital screens");
        ValidatePercentage(request.GlobalRules.MinDigital, "Minimum digital");

        var orderedBands = request.BudgetBands
            .OrderBy(band => band.Min)
            .ThenBy(band => band.Max)
            .ToArray();

        for (var index = 0; index < orderedBands.Length; index++)
        {
            var band = orderedBands[index];
            if (string.IsNullOrWhiteSpace(TrimToNull(band.Name)))
            {
                throw new InvalidOperationException("Each planning allocation budget band needs a name.");
            }

            if (band.Min < 0m || band.Max <= band.Min)
            {
                throw new InvalidOperationException($"Budget band '{band.Name}' must have a valid min/max range.");
            }

            ValidatePercentage(band.OohTarget, $"Billboards and Digital Screens target for {band.Name}");
            ValidatePercentage(band.BillboardShareOfOoh, $"Billboard share for {band.Name}");
            ValidatePercentage(band.TvMin, $"TV minimum for {band.Name}");

            if (band.RadioRange.Length != 2 || band.DigitalRange.Length != 2)
            {
                throw new InvalidOperationException($"Budget band '{band.Name}' must include exactly two values for both radio and digital ranges.");
            }

            ValidateRange(band.RadioRange, $"Radio range for {band.Name}");
            ValidateRange(band.DigitalRange, $"Digital range for {band.Name}");

            if (band.OohTarget + band.TvMin > 1m)
            {
                throw new InvalidOperationException($"Budget band '{band.Name}' cannot allocate more than 100% across Billboards and Digital Screens and TV.");
            }

            if (index > 0 && band.Min < orderedBands[index - 1].Max)
            {
                throw new InvalidOperationException($"Budget band '{band.Name}' overlaps with another band. Keep band ranges ordered and non-overlapping.");
            }
        }
    }

    private static void ValidateRange(decimal[] range, string label)
    {
        ValidatePercentage(range[0], $"{label} minimum");
        ValidatePercentage(range[1], $"{label} maximum");
        if (range[1] < range[0])
        {
            throw new InvalidOperationException($"{label} must have a maximum greater than or equal to its minimum.");
        }
    }

    private static async Task UpsertPackageBandProfileAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid packageBandId,
        string description,
        string audienceFit,
        string quickBenefit,
        string packagePurpose,
        string includeRadio,
        string includeTv,
        string leadTime,
        decimal? recommendedSpend,
        bool isRecommended,
        string benefitsJson,
        CancellationToken cancellationToken)
    {
        await connection.ExecuteAsync(new CommandDefinition(
            @"
            insert into package_band_profiles (
                package_band_id, description, audience_fit, quick_benefit, package_purpose,
                include_radio, include_tv, lead_time_label, recommended_spend, is_recommended, benefits_json
            )
            values (
                @PackageBandId, @Description, @AudienceFit, @QuickBenefit, @PackagePurpose,
                @IncludeRadio, @IncludeTv, @LeadTime, @RecommendedSpend, @IsRecommended, cast(@BenefitsJson as jsonb)
            )
            on conflict (package_band_id) do update
            set
                description = excluded.description,
                audience_fit = excluded.audience_fit,
                quick_benefit = excluded.quick_benefit,
                package_purpose = excluded.package_purpose,
                include_radio = excluded.include_radio,
                include_tv = excluded.include_tv,
                lead_time_label = excluded.lead_time_label,
                recommended_spend = excluded.recommended_spend,
                is_recommended = excluded.is_recommended,
                benefits_json = excluded.benefits_json,
                updated_at = now();",
            new
            {
                PackageBandId = packageBandId,
                Description = TrimToNull(description) ?? string.Empty,
                AudienceFit = TrimToNull(audienceFit) ?? string.Empty,
                QuickBenefit = TrimToNull(quickBenefit) ?? string.Empty,
                PackagePurpose = TrimToNull(packagePurpose) ?? string.Empty,
                IncludeRadio = NormalizeToken(includeRadio),
                IncludeTv = NormalizeToken(includeTv),
                LeadTime = TrimToNull(leadTime) ?? string.Empty,
                RecommendedSpend = recommendedSpend,
                IsRecommended = isRecommended,
                BenefitsJson = benefitsJson
            },
            transaction: transaction,
            cancellationToken: cancellationToken));
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

    private sealed class AdminGeographyRecord
    {
        public Guid Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string? FallbackLocationsJson { get; set; }
        public int SortOrder { get; set; }
        public bool IsActive { get; set; }
        public Guid? ClusterId { get; set; }
    }

    private sealed class RegionClusterLookupRecord
    {
        public Guid Id { get; set; }
        public string Code { get; set; } = string.Empty;
    }

    private sealed record NormalizedOutletSelection(
        string CoverageType,
        string CatalogHealth,
        IReadOnlyList<string> PrimaryLanguages,
        IReadOnlyList<string> ProvinceCodes,
        IReadOnlyList<string> CityLabels,
        IReadOnlyList<string> AudienceKeywords);
}
