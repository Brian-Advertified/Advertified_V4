using System.Globalization;
using System.Text.Json;
using Advertified.App.Services.Abstractions;

namespace Advertified.App.Services;

public sealed class BroadcastCostNormalizer : IBroadcastCostNormalizer
{
    private readonly BroadcastNormalizationOptions _options;

    public BroadcastCostNormalizer(BroadcastNormalizationOptions? options = null)
    {
        _options = options ?? BroadcastNormalizationOptions.Default();
    }

    public NormalizedCostResult NormalizeRadioPackage(
        string station,
        string? packageName,
        decimal? investmentZar,
        decimal? packageCostZar,
        decimal? costPerMonthZar,
        int? durationMonths)
    {
        if (costPerMonthZar.HasValue && costPerMonthZar.Value > 0)
        {
            return new NormalizedCostResult(costPerMonthZar.Value, costPerMonthZar.Value, "radio_package_monthly", "Package already supplied as monthly cost.");
        }

        var sourceCost = investmentZar ?? packageCostZar;
        if (!sourceCost.HasValue || sourceCost.Value <= 0)
        {
            return NormalizedCostResult.Empty("radio_package");
        }

        if (durationMonths.HasValue && durationMonths.Value > 0)
        {
            return new NormalizedCostResult(
                sourceCost.Value,
                DecimalRound(sourceCost.Value / durationMonths.Value),
                "radio_package_multi_month",
                $"Package cost divided across {durationMonths.Value} month(s).");
        }

        return new NormalizedCostResult(
            sourceCost.Value,
            sourceCost.Value,
            "radio_package_once_or_monthly",
            "Package treated as monthly because no duration was supplied.");
    }

    public NormalizedCostResult NormalizeRadioRate(
        string station,
        string? slotLabel,
        string? groupName,
        decimal rawRateZar,
        RadioNormalizationContext? context = null)
    {
        if (rawRateZar <= 0)
        {
            return NormalizedCostResult.Empty("radio_slot");
        }

        var profile = ResolveRadioProfile(station, slotLabel, groupName, context);
        var monthlyCost = rawRateZar * profile.SpotsPerDay * profile.ActiveDaysPerMonth * profile.DaypartWeight;

        return new NormalizedCostResult(
            rawRateZar,
            DecimalRound(monthlyCost),
            "radio_slot",
            $"{profile.SpotsPerDay} spot(s)/day x {profile.ActiveDaysPerMonth} active day(s)/month x weight {profile.DaypartWeight:0.##}.");
    }

    public NormalizedCostResult NormalizeTvPackage(
        string station,
        string? packageName,
        decimal? investmentZar,
        decimal? packageCostZar,
        decimal? costPerMonthZar,
        int? durationWeeks,
        int? durationMonths)
    {
        if (costPerMonthZar.HasValue && costPerMonthZar.Value > 0)
        {
            return new NormalizedCostResult(costPerMonthZar.Value, costPerMonthZar.Value, "tv_package_monthly", "TV package already supplied as monthly cost.");
        }

        var sourceCost = investmentZar ?? packageCostZar;
        if (!sourceCost.HasValue || sourceCost.Value <= 0)
        {
            return NormalizedCostResult.Empty("tv_package");
        }

        if (durationMonths.HasValue && durationMonths.Value > 0)
        {
            return new NormalizedCostResult(
                sourceCost.Value,
                DecimalRound(sourceCost.Value / durationMonths.Value),
                "tv_package_multi_month",
                $"TV package cost divided across {durationMonths.Value} month(s).");
        }

        if (durationWeeks.HasValue && durationWeeks.Value > 0)
        {
            if (durationWeeks.Value == 4)
            {
                return new NormalizedCostResult(sourceCost.Value, sourceCost.Value, "tv_4_week_package", "4-week package treated as monthly.");
            }

            return new NormalizedCostResult(
                sourceCost.Value,
                DecimalRound(sourceCost.Value * (4m / durationWeeks.Value)),
                "tv_weekly_or_multi_week_package",
                $"TV package normalized from {durationWeeks.Value} week(s) to 4 weeks.");
        }

        return new NormalizedCostResult(
            sourceCost.Value,
            sourceCost.Value,
            "tv_package_once_or_monthly",
            "TV package treated as monthly because no duration was supplied.");
    }

    public NormalizedCostResult NormalizeTvRate(
        string station,
        string? programmeName,
        string? slotLabel,
        string? groupName,
        decimal rawRateZar,
        TvNormalizationContext? context = null)
    {
        if (rawRateZar <= 0)
        {
            return NormalizedCostResult.Empty("tv_slot");
        }

        var profile = ResolveTvProfile(programmeName, slotLabel, groupName, context);
        var monthlyCost = rawRateZar * profile.InsertionsPerMonth;

        return new NormalizedCostResult(
            rawRateZar,
            DecimalRound(monthlyCost),
            profile.CostType,
            $"{profile.InsertionsPerMonth} insertion(s)/month assumed.");
    }

    public decimal? GetClosestMonthlySpendPoint(string mediaType, string station, JsonElement packages, JsonElement pricing)
    {
        var candidates = new List<decimal>();

        if (packages.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in packages.EnumerateArray())
            {
                var durationMonths = GetInt(item, "duration_months") ?? GetDurationMonthsFromName(GetString(item, "name"));
                var durationWeeks = GetInt(item, "duration_weeks") ?? GetDurationWeeksFromName(GetString(item, "name"));

                var result = mediaType.Equals("radio", StringComparison.OrdinalIgnoreCase)
                    ? NormalizeRadioPackage(station, GetString(item, "name"), GetDecimal(item, "investment_zar"), GetDecimal(item, "package_cost_zar"), GetDecimal(item, "cost_per_month_zar"), durationMonths)
                    : NormalizeTvPackage(station, GetString(item, "name"), GetDecimal(item, "investment_zar"), GetDecimal(item, "package_cost_zar"), GetDecimal(item, "cost_per_month_zar"), durationWeeks, durationMonths);

                if (result.MonthlyCostEstimateZar > 0)
                {
                    candidates.Add(result.MonthlyCostEstimateZar);
                }

                if (item.TryGetProperty("elements", out var elements) && elements.ValueKind == JsonValueKind.Array)
                {
                    foreach (var element in elements.EnumerateArray())
                    {
                        var elementResult = mediaType.Equals("radio", StringComparison.OrdinalIgnoreCase)
                            ? NormalizeRadioPackage(station, GetString(element, "name"), GetDecimal(element, "investment_zar"), GetDecimal(element, "package_cost_zar"), GetDecimal(element, "cost_per_month_zar") ?? GetDecimal(item, "cost_per_month_zar"), durationMonths)
                            : NormalizeTvPackage(station, GetString(element, "name"), GetDecimal(element, "investment_zar"), GetDecimal(element, "package_cost_zar"), GetDecimal(element, "cost_per_month_zar") ?? GetDecimal(item, "cost_per_month_zar"), durationWeeks, durationMonths);

                        if (elementResult.MonthlyCostEstimateZar > 0)
                        {
                            candidates.Add(elementResult.MonthlyCostEstimateZar);
                        }
                    }
                }
            }
        }

        if (pricing.ValueKind == JsonValueKind.Object)
        {
            foreach (var group in pricing.EnumerateObject())
            {
                if (group.Value.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                foreach (var slot in group.Value.EnumerateObject())
                {
                    var rate = slot.Value.ValueKind switch
                    {
                        JsonValueKind.Number when slot.Value.TryGetDecimal(out var n) => n,
                        JsonValueKind.String when decimal.TryParse(slot.Value.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var s) => s,
                        _ => 0m
                    };

                    if (rate <= 0m)
                    {
                        continue;
                    }

                    var result = mediaType.Equals("radio", StringComparison.OrdinalIgnoreCase)
                        ? NormalizeRadioRate(station, slot.Name, group.Name, rate)
                        : NormalizeTvRate(station, null, slot.Name, group.Name, rate);

                    if (result.MonthlyCostEstimateZar > 0)
                    {
                        candidates.Add(result.MonthlyCostEstimateZar);
                    }
                }
            }
        }
        else if (pricing.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in pricing.EnumerateArray())
            {
                var rate = GetDecimal(item, "price_zar") ?? GetDecimal(item, "rate_zar");
                if (!rate.HasValue || rate.Value <= 0)
                {
                    continue;
                }

                var result = mediaType.Equals("radio", StringComparison.OrdinalIgnoreCase)
                    ? NormalizeRadioRate(station, GetString(item, "slot") ?? GetString(item, "time"), GetString(item, "group"), rate.Value)
                    : NormalizeTvRate(station, GetString(item, "program") ?? GetString(item, "programme"), GetString(item, "slot") ?? GetString(item, "time"), GetString(item, "group"), rate.Value);

                if (result.MonthlyCostEstimateZar > 0)
                {
                    candidates.Add(result.MonthlyCostEstimateZar);
                }
            }
        }

        return candidates.Count == 0 ? null : candidates.Min();
    }

    private RadioNormalizationProfile ResolveRadioProfile(string station, string? slotLabel, string? groupName, RadioNormalizationContext? context)
    {
        if (context is not null)
        {
            return new RadioNormalizationProfile(context.SpotsPerDay, context.ActiveDaysPerMonth, context.DaypartWeight);
        }

        var label = $"{groupName} {slotLabel}".ToLowerInvariant();
        var stationName = station.ToLowerInvariant();

        if (ContainsAny(label, "06:00", "07:00", "08:00", "breakfast", "drive"))
        {
            return _options.RadioPremiumProfile;
        }

        if (ContainsAny(stationName, "kaya", "jozi", "good hope", "smile", "algoa"))
        {
            return _options.RadioRegionalProfile;
        }

        return _options.RadioDefaultProfile;
    }

    private TvNormalizationProfile ResolveTvProfile(string? programmeName, string? slotLabel, string? groupName, TvNormalizationContext? context)
    {
        if (context is not null)
        {
            return new TvNormalizationProfile(context.InsertionsPerMonth, context.CostType ?? "tv_slot");
        }

        var combined = $"{groupName} {slotLabel} {programmeName}".ToLowerInvariant();

        if (ContainsAny(combined, "weekly"))
        {
            return new TvNormalizationProfile(4, "tv_weekly_package_equivalent");
        }

        if (ContainsAny(combined, "4 week", "4-week"))
        {
            return new TvNormalizationProfile(1, "tv_4_week_package");
        }

        if (ContainsAny(combined, "18:30", "19:30", "20:00", "20:30", "prime"))
        {
            return _options.TvPrimeProfile;
        }

        return _options.TvDefaultProfile;
    }

    private static int? GetDurationMonthsFromName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        if (name.Contains("6 Months", StringComparison.OrdinalIgnoreCase)) return 6;
        if (name.Contains("3 Months", StringComparison.OrdinalIgnoreCase)) return 3;
        if (name.Contains("12 Months", StringComparison.OrdinalIgnoreCase)) return 12;
        if (name.Contains("1 Month", StringComparison.OrdinalIgnoreCase)) return 1;
        return null;
    }

    private static int? GetDurationWeeksFromName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        if (name.Contains("4 Week", StringComparison.OrdinalIgnoreCase)) return 4;
        if (name.Contains("2 Week", StringComparison.OrdinalIgnoreCase)) return 2;
        if (name.Contains("8 Week", StringComparison.OrdinalIgnoreCase)) return 8;
        return null;
    }

    private static bool ContainsAny(string value, params string[] needles) => needles.Any(n => value.Contains(n, StringComparison.OrdinalIgnoreCase));

    private static decimal DecimalRound(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

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

        if (property.ValueKind == JsonValueKind.Number && property.TryGetDecimal(out var number))
        {
            return number;
        }

        if (property.ValueKind == JsonValueKind.String &&
            decimal.TryParse(property.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        return null;
    }

    private static int? GetInt(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        if (property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out var number))
        {
            return number;
        }

        if (property.ValueKind == JsonValueKind.String &&
            int.TryParse(property.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        return null;
    }
}

