using System.Globalization;
using System.Text.Json;
using Advertified.App.Contracts.Campaigns;
using Advertified.App.Services.Abstractions;
using Advertified.App.Support;

namespace Advertified.App.Services;

public sealed class BroadcastPlanningInventorySource : IBroadcastPlanningInventorySource
{
    private readonly IBroadcastInventoryCatalog _broadcastInventoryCatalog;
    private readonly IBroadcastCostNormalizer _costNormalizer;
    private readonly IPricingSettingsProvider _pricingSettingsProvider;
    private readonly IBroadcastInventoryIntelligenceService _broadcastInventoryIntelligenceService;
    private readonly ICommercialFlightPricingResolver _commercialFlightPricingResolver;

    public BroadcastPlanningInventorySource(
        IBroadcastInventoryCatalog broadcastInventoryCatalog,
        IBroadcastCostNormalizer costNormalizer,
        IPricingSettingsProvider pricingSettingsProvider,
        IBroadcastInventoryIntelligenceService broadcastInventoryIntelligenceService,
        ICommercialFlightPricingResolver commercialFlightPricingResolver)
    {
        _broadcastInventoryCatalog = broadcastInventoryCatalog;
        _costNormalizer = costNormalizer;
        _pricingSettingsProvider = pricingSettingsProvider;
        _broadcastInventoryIntelligenceService = broadcastInventoryIntelligenceService;
        _commercialFlightPricingResolver = commercialFlightPricingResolver;
    }

    public BroadcastPlanningInventorySource(
        IBroadcastInventoryCatalog broadcastInventoryCatalog,
        IBroadcastCostNormalizer costNormalizer,
        IPricingSettingsProvider pricingSettingsProvider)
        : this(
            broadcastInventoryCatalog,
            costNormalizer,
            pricingSettingsProvider,
            new NullBroadcastInventoryIntelligenceService(),
            new CommercialFlightPricingResolver())
    {
    }

    public async Task<BroadcastPlanningCandidateSet> GetCandidatesAsync(CampaignPlanningRequest request, CancellationToken cancellationToken)
    {
        var records = await _broadcastInventoryCatalog.GetRecordsAsync(cancellationToken);
        var pricingSettings = await _pricingSettingsProvider.GetCurrentAsync(cancellationToken);
        var radioIntelligence = await _broadcastInventoryIntelligenceService.GetRadioIntelligenceByInternalKeyAsync(cancellationToken);
        var tvIntelligence = await _broadcastInventoryIntelligenceService.GetTvIntelligenceByInternalKeyAsync(cancellationToken);

        return new BroadcastPlanningCandidateSet(
            RadioSlots: records
                .Where(record => string.Equals(record.MediaType, "radio", StringComparison.OrdinalIgnoreCase))
                .SelectMany(record => CreateBroadcastRateCandidates(request, record, pricingSettings, radioIntelligence))
                .Where(candidate => candidate.Cost > 0m && candidate.Cost <= request.SelectedBudget)
                .ToList(),
            RadioPackages: records
                .Where(record => string.Equals(record.MediaType, "radio", StringComparison.OrdinalIgnoreCase))
                .SelectMany(record => CreateBroadcastPackageCandidates(request, record, pricingSettings, radioIntelligence))
                .Where(candidate => candidate.Cost > 0m && candidate.Cost <= request.SelectedBudget)
                .ToList(),
            Tv: records
                .Where(record => string.Equals(record.MediaType, "tv", StringComparison.OrdinalIgnoreCase))
                .SelectMany(record => CreateBroadcastPackageCandidates(request, record, pricingSettings, tvIntelligence).Concat(CreateBroadcastRateCandidates(request, record, pricingSettings, tvIntelligence)))
                .Where(candidate => candidate.Cost > 0m && candidate.Cost <= request.SelectedBudget)
                .ToList(),
            Newspapers: records
                .Where(IsNewspaperRecord)
                .SelectMany(record => CreateBroadcastPackageCandidates(request, record, pricingSettings, new Dictionary<string, IReadOnlyDictionary<string, object?>>()))
                .Concat(records
                    .Where(IsNewspaperRecord)
                    .SelectMany(record => CreateBroadcastRateCandidates(request, record, pricingSettings, new Dictionary<string, IReadOnlyDictionary<string, object?>>())))
                .Where(candidate => candidate.Cost > 0m && candidate.Cost <= request.SelectedBudget)
                .ToList());
    }

    public async Task<List<BroadcastPlanningInventorySeed>> GetRadioSlotCandidatesAsync(CampaignPlanningRequest request, CancellationToken cancellationToken)
    {
        var candidates = await GetCandidatesAsync(request, cancellationToken);
        return candidates.RadioSlots;
    }

    public async Task<List<BroadcastPlanningInventorySeed>> GetRadioPackageCandidatesAsync(CampaignPlanningRequest request, CancellationToken cancellationToken)
    {
        var candidates = await GetCandidatesAsync(request, cancellationToken);
        return candidates.RadioPackages;
    }

    public async Task<List<BroadcastPlanningInventorySeed>> GetTvCandidatesAsync(CampaignPlanningRequest request, CancellationToken cancellationToken)
    {
        var candidates = await GetCandidatesAsync(request, cancellationToken);
        return candidates.Tv;
    }

    public async Task<List<BroadcastPlanningInventorySeed>> GetNewspaperCandidatesAsync(CampaignPlanningRequest request, CancellationToken cancellationToken)
    {
        var candidates = await GetCandidatesAsync(request, cancellationToken);
        return candidates.Newspapers;
    }

    private IEnumerable<BroadcastPlanningInventorySeed> CreateBroadcastPackageCandidates(
        CampaignPlanningRequest request,
        BroadcastInventoryRecord record,
        PricingSettingsSnapshot pricingSettings,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, object?>> intelligenceLookup)
    {
        if (record.Packages.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        var index = 0;
        foreach (var package in record.Packages.EnumerateArray())
        {
            var packageName = GetString(package, "name") ?? "Package";
            var packageType = GetString(package, "package_type") ?? InferPackageType(packageName, GetString(package, "notes"));

            if (package.TryGetProperty("elements", out var elements) && elements.ValueKind == JsonValueKind.Array)
            {
                foreach (var element in elements.EnumerateArray())
                {
                    var cost = GetDecimal(element, "investment_zar")
                        ?? GetDecimal(element, "package_cost_zar")
                        ?? GetDecimal(element, "cost_per_month_zar")
                        ?? 0m;
                    var durationMonths = GetInt(element, "duration_months")
                        ?? GetInt(package, "duration_months")
                        ?? GetDurationMonthsFromName(GetString(element, "name"))
                        ?? GetDurationMonthsFromName(packageName);
                    var durationWeeks = GetInt(element, "duration_weeks")
                        ?? GetInt(package, "duration_weeks")
                        ?? GetDurationWeeksFromName(GetString(element, "name"))
                        ?? GetDurationWeeksFromName(packageName);

                    var normalized = NormalizePackageCost(
                        record,
                        GetString(element, "name"),
                        GetDecimal(element, "investment_zar"),
                        GetDecimal(element, "package_cost_zar"),
                        GetDecimal(element, "cost_per_month_zar") ?? GetDecimal(package, "cost_per_month_zar"),
                        durationWeeks,
                        durationMonths);

                    if (normalized.MonthlyCostEstimateZar <= 0m)
                    {
                        continue;
                    }

                    var markedUpComparableMonthlyCost = PricingPolicy.ApplyMarkup(normalized.MonthlyCostEstimateZar, record.MediaType, packageType, pricingSettings);
                    var markedUpRawCost = PricingPolicy.ApplyMarkup(normalized.RawCostZar, record.MediaType, packageType, pricingSettings);
                    var commercialResolution = _commercialFlightPricingResolver.Resolve(
                        request,
                        record.MediaType,
                        ResolvePricingModel(normalized, durationWeeks, durationMonths, packageOnly: true),
                        markedUpRawCost,
                        markedUpComparableMonthlyCost,
                        durationWeeks,
                        durationMonths,
                        packageOnly: true,
                        allowsProration: normalized.CostType.Contains("monthly", StringComparison.OrdinalIgnoreCase));

                    if (commercialResolution.QuotedCost <= 0m)
                    {
                        continue;
                    }

                    var intelligenceKey = $"{record.Id}|package|{GetString(element, "name") ?? packageName}";
                    yield return new BroadcastPlanningInventorySeed
                    {
                        Record = record,
                        SourceId = CreateDeterministicGuid($"{record.Id}:package:{index}:{GetString(element, "name") ?? "element"}"),
                        SourceType = $"{GetSourceTypePrefix(record)}_package",
                        DisplayName = $"{record.Station} - {packageName} - {GetString(element, "name") ?? "Element"}",
                        SlotType = "package",
                        Cost = commercialResolution.QuotedCost,
                        Metadata = MergeMetadata(CreateMetadata(
                            record,
                            normalized,
                            commercialResolution,
                            pricingSettings,
                            packageType,
                            null,
                            null,
                            true,
                            durationWeeks,
                            durationMonths,
                            GetString(element, "name") ?? packageName,
                            GetString(element, "notes") ?? GetString(package, "notes")),
                            ResolveIntelligence(intelligenceLookup, intelligenceKey))
                    };
                }

                index++;
                continue;
            }

            var durationMonthsForPackage = GetInt(package, "duration_months") ?? GetDurationMonthsFromName(packageName);
            var durationWeeksForPackage = GetInt(package, "duration_weeks") ?? GetDurationWeeksFromName(packageName);
            var normalizedPackage = NormalizePackageCost(
                record,
                packageName,
                GetDecimal(package, "investment_zar") ?? GetDecimal(package, "total_investment_zar"),
                GetDecimal(package, "package_cost_zar"),
                GetDecimal(package, "cost_per_month_zar"),
                durationWeeksForPackage,
                durationMonthsForPackage);

            if (normalizedPackage.MonthlyCostEstimateZar <= 0m)
            {
                index++;
                continue;
            }

            var markedUpComparableMonthlyPackageCost = PricingPolicy.ApplyMarkup(normalizedPackage.MonthlyCostEstimateZar, record.MediaType, packageType, pricingSettings);
            var markedUpRawPackageCost = PricingPolicy.ApplyMarkup(normalizedPackage.RawCostZar, record.MediaType, packageType, pricingSettings);
            var packageResolution = _commercialFlightPricingResolver.Resolve(
                request,
                record.MediaType,
                ResolvePricingModel(normalizedPackage, durationWeeksForPackage, durationMonthsForPackage, packageOnly: true),
                markedUpRawPackageCost,
                markedUpComparableMonthlyPackageCost,
                durationWeeksForPackage,
                durationMonthsForPackage,
                packageOnly: true,
                allowsProration: normalizedPackage.CostType.Contains("monthly", StringComparison.OrdinalIgnoreCase));
            if (packageResolution.QuotedCost <= 0m)
            {
                index++;
                continue;
            }

            var packageIntelligenceKey = $"{record.Id}|package|{packageName}";
            yield return new BroadcastPlanningInventorySeed
            {
                Record = record,
                SourceId = CreateDeterministicGuid($"{record.Id}:package:{index}:{packageName}"),
                SourceType = $"{GetSourceTypePrefix(record)}_package",
                DisplayName = $"{record.Station} - {packageName}",
                SlotType = "package",
                Cost = packageResolution.QuotedCost,
                Metadata = MergeMetadata(
                    CreateMetadata(record, normalizedPackage, packageResolution, pricingSettings, packageType, null, null, true, durationWeeksForPackage, durationMonthsForPackage, packageName, GetString(package, "notes")),
                    ResolveIntelligence(intelligenceLookup, packageIntelligenceKey))
            };
            index++;
        }
    }

    private IEnumerable<BroadcastPlanningInventorySeed> CreateBroadcastRateCandidates(
        CampaignPlanningRequest request,
        BroadcastInventoryRecord record,
        PricingSettingsSnapshot pricingSettings,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, object?>> intelligenceLookup)
    {
        if (record.Pricing.ValueKind == JsonValueKind.Object)
        {
            foreach (var dayGroup in record.Pricing.EnumerateObject())
            {
                if (dayGroup.Value.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                foreach (var slot in dayGroup.Value.EnumerateObject())
                {
                    if (!TryGetRate(slot.Value, out var rate))
                    {
                        continue;
                    }

                    var normalized = ResolveRateCost(record, null, slot.Name, dayGroup.Name, rate);

                    if (normalized.MonthlyCostEstimateZar <= 0m)
                    {
                        continue;
                    }

                    var markedUpComparableMonthlyRate = PricingPolicy.ApplyMarkup(normalized.MonthlyCostEstimateZar, record.MediaType, slot.Name, pricingSettings);
                    var commercialResolution = _commercialFlightPricingResolver.Resolve(
                        request,
                        record.MediaType,
                        "monthly_equivalent",
                        markedUpComparableMonthlyRate,
                        markedUpComparableMonthlyRate,
                        null,
                        null,
                        packageOnly: false,
                        allowsProration: true);
                    if (commercialResolution.QuotedCost <= 0m)
                    {
                        continue;
                    }

                    var intelligenceKey = $"{record.Id}|slot|{dayGroup.Name}|{slot.Name}";
                    yield return new BroadcastPlanningInventorySeed
                    {
                        Record = record,
                        SourceId = CreateDeterministicGuid($"{record.Id}:rate:{dayGroup.Name}:{slot.Name}"),
                        SourceType = $"{GetSourceTypePrefix(record)}_slot",
                        DisplayName = $"{record.Station} - {slot.Name}",
                        SlotType = "spot",
                        Cost = commercialResolution.QuotedCost,
                        Metadata = MergeMetadata(
                            CreateMetadata(record, normalized, commercialResolution, pricingSettings, "spot", dayGroup.Name, slot.Name, false, null, null, null, null),
                            ResolveIntelligence(intelligenceLookup, intelligenceKey))
                    };
                }
            }
        }
        else if (record.Pricing.ValueKind == JsonValueKind.Array)
        {
            foreach (var row in record.Pricing.EnumerateArray())
            {
                if (!TryGetRateFromArrayRow(row, out var rate))
                {
                    continue;
                }

                var slotLabel = GetString(row, "slot")
                    ?? GetString(row, "time")
                    ?? BuildSlotLabelFromTimes(GetString(row, "start_time"), GetString(row, "end_time"))
                    ?? "selected slot";
                var dayGroup = GetString(row, "group") ?? GetString(row, "day_group") ?? "schedule";
                var programmeName = GetString(row, "program") ?? GetString(row, "programme") ?? GetString(row, "rate_type");

                var normalized = ResolveRateCost(record, programmeName, slotLabel, dayGroup, rate);

                if (normalized.MonthlyCostEstimateZar <= 0m)
                {
                    continue;
                }

                var markupKey = programmeName ?? slotLabel;
                var markedUpComparableMonthlyRate = PricingPolicy.ApplyMarkup(normalized.MonthlyCostEstimateZar, record.MediaType, markupKey, pricingSettings);
                var commercialResolution = _commercialFlightPricingResolver.Resolve(
                    request,
                    record.MediaType,
                    "monthly_equivalent",
                    markedUpComparableMonthlyRate,
                    markedUpComparableMonthlyRate,
                    null,
                    null,
                    packageOnly: false,
                    allowsProration: true);
                if (commercialResolution.QuotedCost <= 0m)
                {
                    continue;
                }

                var intelligenceKey = $"{record.Id}|slot|{dayGroup}|{slotLabel}";
                var displaySlot = string.IsNullOrWhiteSpace(programmeName) ? slotLabel : $"{programmeName} ({slotLabel})";
                yield return new BroadcastPlanningInventorySeed
                {
                    Record = record,
                    SourceId = CreateDeterministicGuid($"{record.Id}:rate:{dayGroup}:{slotLabel}:{programmeName}:{rate}"),
                    SourceType = $"{GetSourceTypePrefix(record)}_slot",
                    DisplayName = $"{record.Station} - {displaySlot}",
                    SlotType = "spot",
                    Cost = commercialResolution.QuotedCost,
                    Metadata = MergeMetadata(
                        CreateMetadata(record, normalized, commercialResolution, pricingSettings, "spot", dayGroup, slotLabel, false, null, null, null, null),
                        ResolveIntelligence(intelligenceLookup, intelligenceKey))
                };
            }
        }
    }

    private static Dictionary<string, object?> CreateMetadata(
        BroadcastInventoryRecord record,
        NormalizedCostResult normalizedCost,
        CommercialPriceResolution commercialResolution,
        PricingSettingsSnapshot pricingSettings,
        string pricingModel,
        string? dayType,
        string? timeBand,
        bool packageOnly,
        int? offerDurationWeeks,
        int? offerDurationMonths,
        string? packageName,
        string? notes)
    {
        var requestedFlight = commercialResolution.RequestedDurationLabel;
        return new Dictionary<string, object?>
        {
            ["sourceType"] = packageOnly
                ? $"{GetSourceTypePrefix(record)}_package"
                : $"{GetSourceTypePrefix(record)}_slot",
            ["mediaType"] = NormalizeMediaType(record.MediaType),
            ["pricingModel"] = commercialResolution.AppliedPricingModel,
            ["rawCostZar"] = normalizedCost.RawCostZar,
            ["raw_cost_zar"] = normalizedCost.RawCostZar,
            ["monthlyCostEstimateZar"] = commercialResolution.ComparableMonthlyCost,
            ["monthly_cost_estimate_zar"] = commercialResolution.ComparableMonthlyCost,
            ["quotedCostZar"] = commercialResolution.QuotedCost,
            ["quoted_cost_zar"] = commercialResolution.QuotedCost,
            ["markupPercent"] = PricingPolicy.ResolveMarkupPercent(record.MediaType, packageName ?? timeBand, pricingSettings),
            ["markup_percent"] = PricingPolicy.ResolveMarkupPercent(record.MediaType, packageName ?? timeBand, pricingSettings),
            ["costType"] = normalizedCost.CostType,
            ["cost_type"] = normalizedCost.CostType,
            ["normalizationNote"] = normalizedCost.NormalizationNote,
            ["normalization_note"] = normalizedCost.NormalizationNote,
            ["requestedDurationLabel"] = requestedFlight,
            ["requested_duration_label"] = requestedFlight,
            ["appliedDurationLabel"] = commercialResolution.AppliedDurationLabel,
            ["applied_duration_label"] = commercialResolution.AppliedDurationLabel,
            ["requestedMonthsEquivalent"] = commercialResolution.RequestedMonthsEquivalent,
            ["requested_months_equivalent"] = commercialResolution.RequestedMonthsEquivalent,
            ["appliedMonthsEquivalent"] = commercialResolution.AppliedMonthsEquivalent,
            ["applied_months_equivalent"] = commercialResolution.AppliedMonthsEquivalent,
            ["durationFitScore"] = commercialResolution.DurationFitScore,
            ["duration_fit_score"] = commercialResolution.DurationFitScore,
            ["commercialPenalty"] = commercialResolution.CommercialPenalty,
            ["commercial_penalty"] = commercialResolution.CommercialPenalty,
            ["commercialExplanation"] = commercialResolution.Explanation,
            ["commercial_explanation"] = commercialResolution.Explanation,
            ["requestedStartDate"] = commercialResolution.RequestedStartDate,
            ["requested_start_date"] = commercialResolution.RequestedStartDate,
            ["requestedEndDate"] = commercialResolution.RequestedEndDate,
            ["requested_end_date"] = commercialResolution.RequestedEndDate,
            ["resolvedStartDate"] = commercialResolution.ResolvedStartDate,
            ["resolved_start_date"] = commercialResolution.ResolvedStartDate,
            ["resolvedEndDate"] = commercialResolution.ResolvedEndDate,
            ["resolved_end_date"] = commercialResolution.ResolvedEndDate,
            ["offerDurationWeeks"] = offerDurationWeeks,
            ["offer_duration_weeks"] = offerDurationWeeks,
            ["offerDurationMonths"] = offerDurationMonths,
            ["offer_duration_months"] = offerDurationMonths,
            ["rateBasis"] = packageOnly ? "package" : "per_spot",
            ["province"] = record.ProvinceCodes.FirstOrDefault(),
            ["city"] = record.CityLabels.FirstOrDefault(),
            ["area"] = record.CityLabels.FirstOrDefault() ?? record.ProvinceCodes.FirstOrDefault(),
            ["provinceCodes"] = record.ProvinceCodes,
            ["province_codes"] = record.ProvinceCodes,
            ["cityLabels"] = record.CityLabels,
            ["city_labels"] = record.CityLabels,
            ["language"] = record.LanguageDisplay ?? string.Join("/", record.PrimaryLanguages),
            ["timeBand"] = timeBand,
            ["time_band"] = timeBand,
            ["dayType"] = dayType,
            ["day_type"] = dayType,
            ["showDaypart"] = dayType,
            ["show_daypart"] = dayType,
            ["daypart"] = dayType,
            ["slotType"] = packageOnly ? "package" : "spot",
            ["slot_type"] = packageOnly ? "package" : "spot",
            ["regionClusterCode"] = record.ProvinceCodes.FirstOrDefault(),
            ["region_cluster_code"] = record.ProvinceCodes.FirstOrDefault(),
            ["marketScope"] = record.CoverageType,
            ["market_scope"] = record.CoverageType,
            ["marketTier"] = record.CatalogHealth,
            ["market_tier"] = record.CatalogHealth,
            ["packageName"] = packageName,
            ["package_name"] = packageName,
            ["notes"] = notes,
            ["listenershipDaily"] = record.ListenershipDaily,
            ["listenership_daily"] = record.ListenershipDaily,
            ["listenershipWeekly"] = record.ListenershipWeekly,
            ["listenership_weekly"] = record.ListenershipWeekly,
            ["listenershipPeriod"] = record.ListenershipPeriod,
            ["listenership_period"] = record.ListenershipPeriod,
            ["audienceAgeSkew"] = record.AudienceAgeSkew,
            ["audience_age_skew"] = record.AudienceAgeSkew,
            ["audienceGenderSkew"] = record.AudienceGenderSkew,
            ["audience_gender_skew"] = record.AudienceGenderSkew,
            ["audienceLsmRange"] = record.AudienceLsmRange,
            ["audience_lsm_range"] = record.AudienceLsmRange,
            ["urbanRuralMix"] = record.UrbanRuralMix,
            ["urban_rural_mix"] = record.UrbanRuralMix,
            ["targetAudience"] = record.TargetAudience,
            ["target_audience"] = record.TargetAudience,
            ["audienceKeywords"] = record.AudienceKeywords,
            ["audience_keywords"] = record.AudienceKeywords,
            ["buyingBehaviourFit"] = record.BuyingBehaviourFit,
            ["buying_behaviour_fit"] = record.BuyingBehaviourFit,
            ["pricePositioningFit"] = record.PricePositioningFit,
            ["price_positioning_fit"] = record.PricePositioningFit,
            ["salesModelFit"] = record.SalesModelFit,
            ["sales_model_fit"] = record.SalesModelFit,
            ["objectiveFitPrimary"] = record.ObjectiveFitPrimary,
            ["objective_fit_primary"] = record.ObjectiveFitPrimary,
            ["objectiveFitSecondary"] = record.ObjectiveFitSecondary,
            ["objective_fit_secondary"] = record.ObjectiveFitSecondary,
            ["environmentType"] = record.EnvironmentType,
            ["environment_type"] = record.EnvironmentType,
            ["premiumMassFit"] = record.PremiumMassFit,
            ["premium_mass_fit"] = record.PremiumMassFit,
            ["dataConfidence"] = record.DataConfidence,
            ["data_confidence"] = record.DataConfidence,
            ["inventoryIntelligenceNotes"] = record.IntelligenceNotes,
            ["inventory_intelligence_notes"] = record.IntelligenceNotes
        };
    }

    private static string ResolvePricingModel(NormalizedCostResult normalizedCost, int? durationWeeks, int? durationMonths, bool packageOnly)
    {
        if (normalizedCost.CostType.Contains("monthly", StringComparison.OrdinalIgnoreCase))
        {
            return "monthly";
        }

        if (packageOnly && (durationWeeks.HasValue || durationMonths.HasValue))
        {
            return "fixed_term_package";
        }

        return packageOnly ? "package_total" : "monthly_equivalent";
    }

    private static string NormalizeMediaType(string mediaType)
    {
        return mediaType.Trim().ToLowerInvariant() switch
        {
            "tv" => "TV",
            "radio" => "Radio",
            "print" => "Newspaper",
            "newspaper" => "Newspaper",
            "ooh" => "OOH",
            _ => mediaType
        };
    }

    private NormalizedCostResult ResolveRateCost(
        BroadcastInventoryRecord record,
        string? programmeName,
        string? slotLabel,
        string? dayGroup,
        decimal rate)
    {
        if (record.MediaType.Equals("tv", StringComparison.OrdinalIgnoreCase))
        {
            return _costNormalizer.NormalizeTvRate(record.Station, programmeName, slotLabel, dayGroup, rate);
        }

        if (IsNewspaperRecord(record))
        {
            return _costNormalizer.NormalizeNewspaperRate(record.Station, programmeName ?? slotLabel, dayGroup, rate);
        }

        return _costNormalizer.NormalizeRadioRate(record.Station, slotLabel, dayGroup, rate);
    }

    private NormalizedCostResult NormalizePackageCost(
        BroadcastInventoryRecord record,
        string? packageName,
        decimal? investmentZar,
        decimal? packageCostZar,
        decimal? costPerMonthZar,
        int? durationWeeks,
        int? durationMonths)
    {
        if (record.MediaType.Equals("tv", StringComparison.OrdinalIgnoreCase))
        {
            return _costNormalizer.NormalizeTvPackage(
                record.Station,
                packageName,
                investmentZar,
                packageCostZar,
                costPerMonthZar,
                durationWeeks,
                durationMonths);
        }

        if (IsNewspaperRecord(record))
        {
            return _costNormalizer.NormalizeNewspaperPackage(
                record.Station,
                packageName,
                investmentZar,
                packageCostZar,
                costPerMonthZar,
                durationWeeks,
                durationMonths);
        }

        return _costNormalizer.NormalizeRadioPackage(
            record.Station,
            packageName,
            investmentZar,
            packageCostZar,
            costPerMonthZar,
            durationMonths);
    }

    private static bool IsNewspaperRecord(BroadcastInventoryRecord record)
        => record.MediaType.Equals("newspaper", StringComparison.OrdinalIgnoreCase)
           || record.MediaType.Equals("print", StringComparison.OrdinalIgnoreCase);

    private static string GetSourceTypePrefix(BroadcastInventoryRecord record)
    {
        if (record.MediaType.Equals("tv", StringComparison.OrdinalIgnoreCase))
        {
            return "tv";
        }

        if (IsNewspaperRecord(record))
        {
            return "newspaper";
        }

        return "radio";
    }

    private static bool TryGetRate(JsonElement element, out decimal rate)
    {
        rate = 0m;
        return element.ValueKind switch
        {
            JsonValueKind.Number => element.TryGetDecimal(out rate),
            JsonValueKind.String => decimal.TryParse(element.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out rate),
            _ => false
        };
    }

    private static bool TryGetRateFromArrayRow(JsonElement row, out decimal rate)
    {
        rate = 0m;
        if (row.TryGetProperty("price_zar", out var price) && TryGetRate(price, out rate))
        {
            return true;
        }

        if (row.TryGetProperty("rate_zar", out var rateZar) && TryGetRate(rateZar, out rate))
        {
            return true;
        }

        return false;
    }

    private static string? BuildSlotLabelFromTimes(string? startTime, string? endTime)
    {
        if (string.IsNullOrWhiteSpace(startTime) || string.IsNullOrWhiteSpace(endTime))
        {
            return null;
        }

        return $"{startTime.Trim()}-{endTime.Trim()}";
    }

    private static Guid CreateDeterministicGuid(string input)
    {
        var bytes = System.Security.Cryptography.MD5.HashData(System.Text.Encoding.UTF8.GetBytes(input));
        return new Guid(bytes);
    }

    private static string InferPackageType(string? packageName, string? notes)
    {
        var text = $"{packageName} {notes}".Trim().ToLowerInvariant();
        if (text.Contains("sponsorship")) return "sponsorship";
        if (text.Contains("pre-roll") || text.Contains("preroll")) return "preroll";
        if (text.Contains("mixed")) return "mixed";
        return "generic";
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
            JsonValueKind.String when decimal.TryParse(property.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var value) => value,
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
            JsonValueKind.String when int.TryParse(property.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) => value,
            _ => null
        };
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

    private static IReadOnlyDictionary<string, object?> ResolveIntelligence(
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, object?>> lookup,
        string internalKey)
    {
        return lookup.TryGetValue(internalKey, out var metadata)
            ? metadata
            : new Dictionary<string, object?>();
    }

    private static Dictionary<string, object?> MergeMetadata(
        Dictionary<string, object?> baseMetadata,
        IReadOnlyDictionary<string, object?> intelligence)
    {
        if (intelligence.Count == 0)
        {
            return baseMetadata;
        }

        foreach (var pair in intelligence)
        {
            if (pair.Value is not null)
            {
                baseMetadata[pair.Key] = pair.Value;
            }
        }

        return baseMetadata;
    }

    private sealed class NullBroadcastInventoryIntelligenceService : IBroadcastInventoryIntelligenceService
    {
        public Task<IReadOnlyDictionary<string, IReadOnlyDictionary<string, object?>>> GetRadioIntelligenceByInternalKeyAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyDictionary<string, IReadOnlyDictionary<string, object?>>>(new Dictionary<string, IReadOnlyDictionary<string, object?>>());

        public Task<IReadOnlyDictionary<string, IReadOnlyDictionary<string, object?>>> GetTvIntelligenceByInternalKeyAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyDictionary<string, IReadOnlyDictionary<string, object?>>>(new Dictionary<string, IReadOnlyDictionary<string, object?>>());
    }
}
