using System.Text.Json;
using Advertified.App.Contracts.Campaigns;
using Advertified.App.Services.Abstractions;
using Advertified.App.Support;

namespace Advertified.App.Services;

public sealed class SocialPlanningInventorySource : ISocialPlanningInventorySource
{
    private readonly IBroadcastInventoryCatalog _inventoryCatalog;
    private readonly IPricingSettingsProvider _pricingSettingsProvider;

    public SocialPlanningInventorySource(
        IBroadcastInventoryCatalog inventoryCatalog,
        IPricingSettingsProvider pricingSettingsProvider)
    {
        _inventoryCatalog = inventoryCatalog;
        _pricingSettingsProvider = pricingSettingsProvider;
    }

    public async Task<List<BroadcastPlanningInventorySeed>> GetCandidatesAsync(CampaignPlanningRequest request, CancellationToken cancellationToken)
    {
        var records = await _inventoryCatalog.GetRecordsAsync(cancellationToken);
        var pricingSettings = await _pricingSettingsProvider.GetCurrentAsync(cancellationToken);

        return records
            .Where(record => string.Equals(record.MediaType, "digital", StringComparison.OrdinalIgnoreCase))
            .SelectMany(record => CreateDigitalCandidates(record, pricingSettings))
            .Where(candidate => candidate.Cost > 0m && candidate.Cost <= request.SelectedBudget)
            .ToList();
    }

    private static IEnumerable<BroadcastPlanningInventorySeed> CreateDigitalCandidates(
        BroadcastInventoryRecord record,
        PricingSettingsSnapshot pricingSettings)
    {
        if (record.Packages.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        var index = 0;
        foreach (var package in record.Packages.EnumerateArray())
        {
            var packageName = GetString(package, "name") ?? "Benchmark package";
            var notes = GetString(package, "notes");
            var investment = GetDecimal(package, "investment_zar");
            var monthlyCost = GetDecimal(package, "cost_per_month_zar") ?? investment;
            if (!monthlyCost.HasValue || monthlyCost.Value <= 0m)
            {
                index++;
                continue;
            }

            var quotedCost = PricingPolicy.ApplyMarkup(monthlyCost.Value, record.MediaType, packageName, pricingSettings);
            if (quotedCost <= 0m)
            {
                quotedCost = monthlyCost.Value;
            }

            yield return new BroadcastPlanningInventorySeed
            {
                Record = record,
                SourceId = CreateDeterministicGuid($"{record.Id}:digital:{index}:{packageName}"),
                SourceType = "digital_package",
                DisplayName = $"{record.Station} - {packageName}",
                SlotType = "package",
                Cost = quotedCost,
                Metadata = CreateMetadata(record, monthlyCost.Value, quotedCost, packageName, notes)
            };

            index++;
        }
    }

    private static Dictionary<string, object?> CreateMetadata(
        BroadcastInventoryRecord record,
        decimal monthlyCostEstimateZar,
        decimal quotedCost,
        string packageName,
        string? notes)
    {
        return new Dictionary<string, object?>
        {
            ["sourceType"] = "digital_package",
            ["mediaType"] = "digital",
            ["pricingModel"] = "benchmark_package_total",
            ["pricing_model"] = "benchmark_package_total",
            ["rawCostZar"] = monthlyCostEstimateZar,
            ["raw_cost_zar"] = monthlyCostEstimateZar,
            ["monthlyCostEstimateZar"] = monthlyCostEstimateZar,
            ["monthly_cost_estimate_zar"] = monthlyCostEstimateZar,
            ["quotedCostZar"] = quotedCost,
            ["quoted_cost_zar"] = quotedCost,
            ["costType"] = "benchmark_monthly_spend",
            ["cost_type"] = "benchmark_monthly_spend",
            ["rateBasis"] = "benchmark_package",
            ["rate_basis"] = "benchmark_package",
            ["packageName"] = packageName,
            ["package_name"] = packageName,
            ["notes"] = notes,
            ["province"] = record.ProvinceCodes.FirstOrDefault(),
            ["city"] = record.CityLabels.FirstOrDefault(),
            ["area"] = record.CityLabels.FirstOrDefault() ?? record.ProvinceCodes.FirstOrDefault() ?? "National",
            ["provinceCodes"] = record.ProvinceCodes,
            ["province_codes"] = record.ProvinceCodes,
            ["cityLabels"] = record.CityLabels,
            ["city_labels"] = record.CityLabels,
            ["language"] = record.LanguageDisplay ?? string.Join("/", record.PrimaryLanguages),
            ["marketScope"] = record.CoverageType,
            ["market_scope"] = record.CoverageType,
            ["marketTier"] = record.CatalogHealth,
            ["market_tier"] = record.CatalogHealth,
            ["targetAudience"] = record.TargetAudience,
            ["target_audience"] = record.TargetAudience,
            ["audienceKeywords"] = record.AudienceKeywords,
            ["audience_keywords"] = record.AudienceKeywords,
            ["audienceAgeSkew"] = record.AudienceAgeSkew,
            ["audience_age_skew"] = record.AudienceAgeSkew,
            ["audienceGenderSkew"] = record.AudienceGenderSkew,
            ["audience_gender_skew"] = record.AudienceGenderSkew,
            ["audienceLsmRange"] = record.AudienceLsmRange,
            ["audience_lsm_range"] = record.AudienceLsmRange,
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

    private static Guid CreateDeterministicGuid(string input)
    {
        var bytes = System.Security.Cryptography.MD5.HashData(System.Text.Encoding.UTF8.GetBytes(input));
        return new Guid(bytes);
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
            JsonValueKind.Number when property.TryGetDecimal(out var value) => value,
            JsonValueKind.String when decimal.TryParse(property.GetString(), out var value) => value,
            _ => null
        };
    }
}
