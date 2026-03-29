using System.Globalization;
using System.Text.Json;
using Advertified.App.Contracts.Campaigns;
using Advertified.App.Services.Abstractions;

namespace Advertified.App.Services;

public sealed class BroadcastPlanningInventorySource : IBroadcastPlanningInventorySource
{
    private readonly IBroadcastInventoryCatalog _broadcastInventoryCatalog;
    private readonly IBroadcastCostNormalizer _costNormalizer;

    public BroadcastPlanningInventorySource(
        IBroadcastInventoryCatalog broadcastInventoryCatalog,
        IBroadcastCostNormalizer costNormalizer)
    {
        _broadcastInventoryCatalog = broadcastInventoryCatalog;
        _costNormalizer = costNormalizer;
    }

    public async Task<List<BroadcastPlanningInventorySeed>> GetRadioSlotCandidatesAsync(CampaignPlanningRequest request, CancellationToken cancellationToken)
    {
        var records = await _broadcastInventoryCatalog.GetRecordsAsync(cancellationToken);
        return records
            .Where(record => string.Equals(record.MediaType, "radio", StringComparison.OrdinalIgnoreCase))
            .SelectMany(CreateBroadcastRateCandidates)
            .Where(candidate => candidate.Cost > 0m && candidate.Cost <= request.SelectedBudget)
            .ToList();
    }

    public async Task<List<BroadcastPlanningInventorySeed>> GetRadioPackageCandidatesAsync(CampaignPlanningRequest request, CancellationToken cancellationToken)
    {
        var records = await _broadcastInventoryCatalog.GetRecordsAsync(cancellationToken);
        return records
            .Where(record => string.Equals(record.MediaType, "radio", StringComparison.OrdinalIgnoreCase))
            .SelectMany(CreateBroadcastPackageCandidates)
            .Where(candidate => candidate.Cost > 0m && candidate.Cost <= request.SelectedBudget)
            .ToList();
    }

    public async Task<List<BroadcastPlanningInventorySeed>> GetTvCandidatesAsync(CampaignPlanningRequest request, CancellationToken cancellationToken)
    {
        var records = await _broadcastInventoryCatalog.GetRecordsAsync(cancellationToken);
        return records
            .Where(record => string.Equals(record.MediaType, "tv", StringComparison.OrdinalIgnoreCase))
            .SelectMany(record => CreateBroadcastPackageCandidates(record).Concat(CreateBroadcastRateCandidates(record)))
            .Where(candidate => candidate.Cost > 0m && candidate.Cost <= request.SelectedBudget)
            .ToList();
    }

    private IEnumerable<BroadcastPlanningInventorySeed> CreateBroadcastPackageCandidates(BroadcastInventoryRecord record)
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

                    yield return new BroadcastPlanningInventorySeed
                    {
                        Record = record,
                        SourceId = CreateDeterministicGuid($"{record.Id}:package:{index}:{GetString(element, "name") ?? "element"}"),
                        SourceType = record.MediaType.Equals("tv", StringComparison.OrdinalIgnoreCase) ? "tv_package" : "radio_package",
                        DisplayName = $"{record.Station} - {packageName} - {GetString(element, "name") ?? "Element"}",
                        SlotType = "package",
                        Cost = normalized.MonthlyCostEstimateZar,
                        Metadata = CreateMetadata(
                            record,
                            normalized,
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

            yield return new BroadcastPlanningInventorySeed
            {
                Record = record,
                SourceId = CreateDeterministicGuid($"{record.Id}:package:{index}:{packageName}"),
                SourceType = record.MediaType.Equals("tv", StringComparison.OrdinalIgnoreCase) ? "tv_package" : "radio_package",
                DisplayName = $"{record.Station} - {packageName}",
                SlotType = "package",
                Cost = normalizedPackage.MonthlyCostEstimateZar,
                Metadata = CreateMetadata(record, normalizedPackage, packageType, null, null, true, packageName, GetString(package, "notes"))
            };
            index++;
        }
    }

    private IEnumerable<BroadcastPlanningInventorySeed> CreateBroadcastRateCandidates(BroadcastInventoryRecord record)
    {
        if (record.Pricing.ValueKind != JsonValueKind.Object)
        {
            yield break;
        }

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

                yield return new BroadcastPlanningInventorySeed
                {
                    Record = record,
                    SourceId = CreateDeterministicGuid($"{record.Id}:rate:{dayGroup.Name}:{slot.Name}"),
                    SourceType = record.MediaType.Equals("tv", StringComparison.OrdinalIgnoreCase) ? "tv_slot" : "radio_slot",
                    DisplayName = $"{record.Station} - {slot.Name}",
                    SlotType = "spot",
                    Cost = normalized.MonthlyCostEstimateZar,
                    Metadata = CreateMetadata(record, normalized, "spot", dayGroup.Name, slot.Name, false, null, null)
                };
            }
        }
    }

    private static Dictionary<string, object?> CreateMetadata(
        BroadcastInventoryRecord record,
        NormalizedCostResult normalizedCost,
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
            ["costType"] = normalizedCost.CostType,
            ["cost_type"] = normalizedCost.CostType,
            ["normalizationNote"] = normalizedCost.NormalizationNote,
            ["normalization_note"] = normalizedCost.NormalizationNote,
            ["rateBasis"] = packageOnly ? "package" : "per_spot",
            ["province"] = record.ProvinceCodes.FirstOrDefault(),
            ["city"] = record.CityLabels.FirstOrDefault(),
            ["area"] = record.CityLabels.FirstOrDefault() ?? record.ProvinceCodes.FirstOrDefault(),
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
            ["targetAudience"] = record.TargetAudience,
            ["target_audience"] = record.TargetAudience
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
