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

    public BroadcastPlanningInventorySource(
        IBroadcastInventoryCatalog broadcastInventoryCatalog,
        IBroadcastCostNormalizer costNormalizer,
        IPricingSettingsProvider pricingSettingsProvider)
    {
        _broadcastInventoryCatalog = broadcastInventoryCatalog;
        _costNormalizer = costNormalizer;
        _pricingSettingsProvider = pricingSettingsProvider;
    }

    public async Task<BroadcastPlanningCandidateSet> GetCandidatesAsync(CampaignPlanningRequest request, CancellationToken cancellationToken)
    {
        var records = await _broadcastInventoryCatalog.GetRecordsAsync(cancellationToken);
        var pricingSettings = await _pricingSettingsProvider.GetCurrentAsync(cancellationToken);

        return new BroadcastPlanningCandidateSet(
            RadioSlots: records
                .Where(record => string.Equals(record.MediaType, "radio", StringComparison.OrdinalIgnoreCase))
                .SelectMany(record => CreateBroadcastRateCandidates(record, pricingSettings))
                .Where(candidate => candidate.Cost > 0m && candidate.Cost <= request.SelectedBudget)
                .ToList(),
            RadioPackages: records
                .Where(record => string.Equals(record.MediaType, "radio", StringComparison.OrdinalIgnoreCase))
                .SelectMany(record => CreateBroadcastPackageCandidates(record, pricingSettings))
                .Where(candidate => candidate.Cost > 0m && candidate.Cost <= request.SelectedBudget)
                .ToList(),
            Tv: records
                .Where(record => string.Equals(record.MediaType, "tv", StringComparison.OrdinalIgnoreCase))
                .SelectMany(record => CreateBroadcastPackageCandidates(record, pricingSettings).Concat(CreateBroadcastRateCandidates(record, pricingSettings)))
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

    private IEnumerable<BroadcastPlanningInventorySeed> CreateBroadcastPackageCandidates(BroadcastInventoryRecord record, PricingSettingsSnapshot pricingSettings)
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

                    var normalized = record.MediaType.Equals("tv", StringComparison.OrdinalIgnoreCase)
                        ? _costNormalizer.NormalizeTvPackage(
                            record.Station,
                            GetString(element, "name"),
                            GetDecimal(element, "investment_zar"),
                            GetDecimal(element, "package_cost_zar"),
                            GetDecimal(element, "cost_per_month_zar") ?? GetDecimal(package, "cost_per_month_zar"),
                            durationWeeks,
                            durationMonths)
                        : _costNormalizer.NormalizeRadioPackage(
                            record.Station,
                            GetString(element, "name"),
                            GetDecimal(element, "investment_zar"),
                            GetDecimal(element, "package_cost_zar"),
                            GetDecimal(element, "cost_per_month_zar") ?? GetDecimal(package, "cost_per_month_zar"),
                            durationMonths);

                    if (normalized.MonthlyCostEstimateZar <= 0m)
                    {
                        continue;
                    }

                    var markedUpCost = PricingPolicy.ApplyMarkup(normalized.MonthlyCostEstimateZar, record.MediaType, packageType, pricingSettings);
                    if (markedUpCost <= 0m)
                    {
                        continue;
                    }

                    yield return new BroadcastPlanningInventorySeed
                    {
                        Record = record,
                        SourceId = CreateDeterministicGuid($"{record.Id}:package:{index}:{GetString(element, "name") ?? "element"}"),
                        SourceType = record.MediaType.Equals("tv", StringComparison.OrdinalIgnoreCase) ? "tv_package" : "radio_package",
                        DisplayName = $"{record.Station} - {packageName} - {GetString(element, "name") ?? "Element"}",
                        SlotType = "package",
                        Cost = markedUpCost,
                        Metadata = CreateMetadata(
                            record,
                            normalized,
                            markedUpCost,
                            pricingSettings,
                            packageType,
                            null,
                            null,
                            true,
                            GetString(element, "name") ?? packageName,
                            GetString(element, "notes") ?? GetString(package, "notes"))
                    };
                }

                index++;
                continue;
            }

            var packageCost = GetDecimal(package, "investment_zar")
                ?? GetDecimal(package, "total_investment_zar")
                ?? GetDecimal(package, "package_cost_zar")
                ?? GetDecimal(package, "cost_per_month_zar")
                ?? 0m;
            var durationMonthsForPackage = GetInt(package, "duration_months") ?? GetDurationMonthsFromName(packageName);
            var durationWeeksForPackage = GetInt(package, "duration_weeks") ?? GetDurationWeeksFromName(packageName);
            var normalizedPackage = record.MediaType.Equals("tv", StringComparison.OrdinalIgnoreCase)
                ? _costNormalizer.NormalizeTvPackage(
                    record.Station,
                    packageName,
                    GetDecimal(package, "investment_zar") ?? GetDecimal(package, "total_investment_zar"),
                    GetDecimal(package, "package_cost_zar"),
                    GetDecimal(package, "cost_per_month_zar"),
                    durationWeeksForPackage,
                    durationMonthsForPackage)
                : _costNormalizer.NormalizeRadioPackage(
                    record.Station,
                    packageName,
                    GetDecimal(package, "investment_zar") ?? GetDecimal(package, "total_investment_zar"),
                    GetDecimal(package, "package_cost_zar"),
                    GetDecimal(package, "cost_per_month_zar"),
                    durationMonthsForPackage);

            if (normalizedPackage.MonthlyCostEstimateZar <= 0m)
            {
                index++;
                continue;
            }

            var markedUpPackageCost = PricingPolicy.ApplyMarkup(normalizedPackage.MonthlyCostEstimateZar, record.MediaType, packageType, pricingSettings);
            if (markedUpPackageCost <= 0m)
            {
                index++;
                continue;
            }

            yield return new BroadcastPlanningInventorySeed
            {
                Record = record,
                SourceId = CreateDeterministicGuid($"{record.Id}:package:{index}:{packageName}"),
                SourceType = record.MediaType.Equals("tv", StringComparison.OrdinalIgnoreCase) ? "tv_package" : "radio_package",
                DisplayName = $"{record.Station} - {packageName}",
                SlotType = "package",
                Cost = markedUpPackageCost,
                Metadata = CreateMetadata(record, normalizedPackage, markedUpPackageCost, pricingSettings, packageType, null, null, true, packageName, GetString(package, "notes"))
            };
            index++;
        }
    }

    private IEnumerable<BroadcastPlanningInventorySeed> CreateBroadcastRateCandidates(BroadcastInventoryRecord record, PricingSettingsSnapshot pricingSettings)
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

                    var normalized = record.MediaType.Equals("tv", StringComparison.OrdinalIgnoreCase)
                        ? _costNormalizer.NormalizeTvRate(record.Station, null, slot.Name, dayGroup.Name, rate)
                        : _costNormalizer.NormalizeRadioRate(record.Station, slot.Name, dayGroup.Name, rate);

                    if (normalized.MonthlyCostEstimateZar <= 0m)
                    {
                        continue;
                    }

                    var markedUpRate = PricingPolicy.ApplyMarkup(normalized.MonthlyCostEstimateZar, record.MediaType, slot.Name, pricingSettings);
                    if (markedUpRate <= 0m)
                    {
                        continue;
                    }

                    yield return new BroadcastPlanningInventorySeed
                    {
                        Record = record,
                        SourceId = CreateDeterministicGuid($"{record.Id}:rate:{dayGroup.Name}:{slot.Name}"),
                        SourceType = record.MediaType.Equals("tv", StringComparison.OrdinalIgnoreCase) ? "tv_slot" : "radio_slot",
                        DisplayName = $"{record.Station} - {slot.Name}",
                        SlotType = "spot",
                        Cost = markedUpRate,
                        Metadata = CreateMetadata(record, normalized, markedUpRate, pricingSettings, "spot", dayGroup.Name, slot.Name, false, null, null)
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

                var normalized = record.MediaType.Equals("tv", StringComparison.OrdinalIgnoreCase)
                    ? _costNormalizer.NormalizeTvRate(record.Station, programmeName, slotLabel, dayGroup, rate)
                    : _costNormalizer.NormalizeRadioRate(record.Station, slotLabel, dayGroup, rate);

                if (normalized.MonthlyCostEstimateZar <= 0m)
                {
                    continue;
                }

                var markupKey = programmeName ?? slotLabel;
                var markedUpRate = PricingPolicy.ApplyMarkup(normalized.MonthlyCostEstimateZar, record.MediaType, markupKey, pricingSettings);
                if (markedUpRate <= 0m)
                {
                    continue;
                }

                var displaySlot = string.IsNullOrWhiteSpace(programmeName) ? slotLabel : $"{programmeName} ({slotLabel})";
                yield return new BroadcastPlanningInventorySeed
                {
                    Record = record,
                    SourceId = CreateDeterministicGuid($"{record.Id}:rate:{dayGroup}:{slotLabel}:{programmeName}:{rate}"),
                    SourceType = record.MediaType.Equals("tv", StringComparison.OrdinalIgnoreCase) ? "tv_slot" : "radio_slot",
                    DisplayName = $"{record.Station} - {displaySlot}",
                    SlotType = "spot",
                    Cost = markedUpRate,
                    Metadata = CreateMetadata(record, normalized, markedUpRate, pricingSettings, "spot", dayGroup, slotLabel, false, null, null)
                };
            }
        }
    }

    private static Dictionary<string, object?> CreateMetadata(
        BroadcastInventoryRecord record,
        NormalizedCostResult normalizedCost,
        decimal quotedCost,
        PricingSettingsSnapshot pricingSettings,
        string pricingModel,
        string? dayType,
        string? timeBand,
        bool packageOnly,
        string? packageName,
        string? notes)
    {
        return new Dictionary<string, object?>
        {
            ["sourceType"] = packageOnly
                ? (record.MediaType.Equals("tv", StringComparison.OrdinalIgnoreCase) ? "tv_package" : "radio_package")
                : (record.MediaType.Equals("tv", StringComparison.OrdinalIgnoreCase) ? "tv_slot" : "radio_slot"),
            ["mediaType"] = NormalizeMediaType(record.MediaType),
            ["pricingModel"] = pricingModel,
            ["rawCostZar"] = normalizedCost.RawCostZar,
            ["raw_cost_zar"] = normalizedCost.RawCostZar,
            ["monthlyCostEstimateZar"] = normalizedCost.MonthlyCostEstimateZar,
            ["monthly_cost_estimate_zar"] = normalizedCost.MonthlyCostEstimateZar,
            ["quotedCostZar"] = quotedCost,
            ["quoted_cost_zar"] = quotedCost,
            ["markupPercent"] = PricingPolicy.ResolveMarkupPercent(record.MediaType, packageName ?? timeBand, pricingSettings),
            ["markup_percent"] = PricingPolicy.ResolveMarkupPercent(record.MediaType, packageName ?? timeBand, pricingSettings),
            ["costType"] = normalizedCost.CostType,
            ["cost_type"] = normalizedCost.CostType,
            ["normalizationNote"] = normalizedCost.NormalizationNote,
            ["normalization_note"] = normalizedCost.NormalizationNote,
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

    private static string NormalizeMediaType(string mediaType)
    {
        return mediaType.Trim().ToLowerInvariant() switch
        {
            "tv" => "TV",
            "radio" => "Radio",
            "ooh" => "OOH",
            _ => mediaType
        };
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
}
