using Advertified.App.Services.Abstractions;
using System.Globalization;
using System.Text.Json;

namespace Advertified.App.Services;

public sealed class PackagePreviewBroadcastSelector : IPackagePreviewBroadcastSelector
{
    private readonly IBroadcastCostNormalizer _costNormalizer;

    public PackagePreviewBroadcastSelector(IBroadcastCostNormalizer costNormalizer)
    {
        _costNormalizer = costNormalizer;
    }

    public IReadOnlyList<string> BuildRadioSupportExamples(
        IReadOnlyList<BroadcastInventoryRecord> records,
        PackagePreviewAreaProfile selectedArea,
        string bandCode,
        decimal budget,
        decimal budgetRatio)
    {
        var normalizedBandCode = bandCode.Trim().ToLowerInvariant();
        var candidates = records
            .Where(record => string.Equals(record.MediaType, "radio", StringComparison.OrdinalIgnoreCase))
            .Where(record => MatchesArea(record, selectedArea) || IsNationalRecordAllowed(record, normalizedBandCode))
            .Select(record => new
            {
                Record = record,
                Score = ScoreRecord(record, selectedArea, normalizedBandCode, budget, budgetRatio, isTv: false)
            })
            .OrderByDescending(candidate => candidate.Score)
            .ThenByDescending(candidate => candidate.Record.HasPricing)
            .ThenByDescending(candidate => candidate.Record.ListenershipWeekly ?? candidate.Record.ListenershipDaily ?? 0)
            .ThenBy(candidate => candidate.Record.Station, StringComparer.OrdinalIgnoreCase)
            .Take(8)
            .ToList();

        var labels = candidates
            .Select(candidate => BuildRadioSupportLabel(candidate.Record, budget))
            .Where(static label => !string.IsNullOrWhiteSpace(label))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return TakeRandomExamples(labels, 3);
    }

    public IReadOnlyList<string> BuildTvSupportExamples(
        IReadOnlyList<BroadcastInventoryRecord> records,
        PackagePreviewAreaProfile selectedArea,
        string bandCode,
        decimal budget,
        decimal budgetRatio)
    {
        var normalizedBandCode = bandCode.Trim().ToLowerInvariant();
        var candidates = records
            .Where(record => string.Equals(record.MediaType, "tv", StringComparison.OrdinalIgnoreCase))
            .Where(record => selectedArea.Code == "national" || record.IsNational || string.Equals(record.CoverageType, "national", StringComparison.OrdinalIgnoreCase))
            .Select(record => new
            {
                Record = record,
                Score = ScoreRecord(record, selectedArea, normalizedBandCode, budget, budgetRatio, isTv: true)
            })
            .OrderByDescending(candidate => candidate.Score)
            .ThenByDescending(candidate => candidate.Record.HasPricing)
            .ThenBy(candidate => candidate.Record.Station, StringComparer.OrdinalIgnoreCase)
            .Take(6)
            .ToList();

        var labels = candidates
            .Select(candidate => BuildTvSupportLabel(candidate.Record, budget))
            .Where(static label => !string.IsNullOrWhiteSpace(label))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (labels.Count == 0)
        {
            return labels;
        }

        labels = TakeRandomExamples(labels, selectedArea.Code == "national" ? 3 : 2).ToList();

        if (selectedArea.Code != "national")
        {
            labels.Insert(0, "TV can be included for broader or national campaigns at this package level");
        }

        return labels
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(3)
            .ToList();
    }

    private static IReadOnlyList<string> TakeRandomExamples(IReadOnlyList<string> labels, int count)
    {
        if (labels.Count <= count)
        {
            return labels;
        }

        return labels
            .OrderBy(_ => Random.Shared.Next())
            .Take(count)
            .ToList();
    }

    private static bool MatchesArea(BroadcastInventoryRecord record, PackagePreviewAreaProfile selectedArea)
    {
        if (selectedArea.Code == "national")
        {
            return record.IsNational || string.Equals(record.CoverageType, "national", StringComparison.OrdinalIgnoreCase);
        }

        var selectedCode = NormalizeBroadcastToken(selectedArea.Code);
        var provinces = record.ProvinceCodes.Select(NormalizeBroadcastToken).ToList();
        var cities = record.CityLabels.Select(static city => city.Trim().ToLowerInvariant()).ToList();

        if (selectedCode == "kzn" && provinces.Contains("kwazulu_natal", StringComparer.OrdinalIgnoreCase))
        {
            return true;
        }

        if (provinces.Contains(selectedCode, StringComparer.OrdinalIgnoreCase))
        {
            return true;
        }

        return selectedArea.CityTerms.Any(term => cities.Any(city => city.Contains(term, StringComparison.OrdinalIgnoreCase)));
    }

    private static bool IsNationalRecordAllowed(BroadcastInventoryRecord record, string bandCode)
    {
        return (bandCode == "scale" || bandCode == "dominance")
            && (record.IsNational || string.Equals(record.CoverageType, "national", StringComparison.OrdinalIgnoreCase));
    }

    private decimal ScoreRecord(
        BroadcastInventoryRecord record,
        PackagePreviewAreaProfile selectedArea,
        string bandCode,
        decimal budget,
        decimal budgetRatio,
        bool isTv)
    {
        var score = 0m;

        if (MatchesArea(record, selectedArea))
        {
            score += 18m;
        }
        else if (record.IsNational || string.Equals(record.CoverageType, "national", StringComparison.OrdinalIgnoreCase))
        {
            score += bandCode is "scale" or "dominance" ? 10m : 2m;
        }

        if (record.HasPricing)
        {
            score += 8m;
        }

        score += record.CatalogHealth switch
        {
            "strong" => 8m,
            "mixed" => 4m,
            "weak_partial_pricing" => 2m,
            _ => 0m
        };

        score += string.Equals(record.CoverageType, "national", StringComparison.OrdinalIgnoreCase)
            ? (bandCode is "scale" or "dominance" ? 5m : -1m)
            : string.Equals(record.CoverageType, "regional", StringComparison.OrdinalIgnoreCase)
                ? 3m
                : 1m;

        if (record.ListenershipWeekly.HasValue)
        {
            score += Math.Min(10m, record.ListenershipWeekly.Value / 150000m);
        }
        else if (record.ListenershipDaily.HasValue)
        {
            score += Math.Min(8m, record.ListenershipDaily.Value / 75000m);
        }

        if (!isTv)
        {
            if (record.AudienceKeywords.Any(keyword => keyword.Contains("music", StringComparison.OrdinalIgnoreCase)
                || keyword.Contains("lifestyle", StringComparison.OrdinalIgnoreCase)
                || keyword.Contains("commuter", StringComparison.OrdinalIgnoreCase)))
            {
                score += 3m;
            }
        }
        else if (record.AudienceKeywords.Any(keyword => keyword.Contains("news", StringComparison.OrdinalIgnoreCase)
            || keyword.Contains("sport", StringComparison.OrdinalIgnoreCase)))
        {
            score += 3m;
        }

        var pricePoint = _costNormalizer.GetClosestMonthlySpendPoint(
            isTv ? "tv" : "radio",
            record.Station,
            record.Packages,
            record.Pricing);
        if (pricePoint.HasValue && pricePoint.Value > 0m)
        {
            var target = isTv ? budget * 0.35m : budget * 0.18m;
            var ratio = target <= 0m ? 1m : pricePoint.Value / target;
            if (ratio <= 1.1m)
            {
                score += 6m;
            }
            else if (ratio <= 1.4m)
            {
                score += 3m;
            }
        }
        else if (budgetRatio < 0.5m)
        {
            score -= 2m;
        }

        return score;
    }

    private string BuildRadioSupportLabel(BroadcastInventoryRecord record, decimal budget)
    {
        var package = EnumeratePackageCandidates(record.Packages)
            .Select(package => new
            {
                Package = package,
                Normalized = _costNormalizer.NormalizeRadioPackage(
                    record.Station,
                    package.Name,
                    package.InvestmentZar,
                    package.PackageCostZar,
                    package.CostPerMonthZar,
                    GetDurationMonths(package.Name))
            })
            .Where(x => x.Normalized.MonthlyCostEstimateZar > 0m)
            .OrderBy(x => Math.Abs(x.Normalized.MonthlyCostEstimateZar - (budget * 0.18m)))
            .FirstOrDefault();

        if (package is not null)
        {
            return $"{record.Station} - {package.Package.Name} ({DescribeAudience(record)})";
        }

        var rate = EnumerateRateCandidates(record.Pricing)
            .Select(rate => new
            {
                Rate = rate,
                Normalized = _costNormalizer.NormalizeRadioRate(record.Station, rate.SlotLabel, rate.GroupName, rate.RateZar)
            })
            .Where(x => x.Normalized.MonthlyCostEstimateZar > 0m)
            .OrderBy(x => Math.Abs(x.Normalized.MonthlyCostEstimateZar - (budget * 0.12m)))
            .FirstOrDefault();

        if (rate is not null)
        {
            var slot = string.IsNullOrWhiteSpace(rate.Rate.ProgrammeName) ? HumanizeDaypart(rate.Rate.SlotLabel) : rate.Rate.ProgrammeName!;
            return $"{record.Station} - {slot} ({DescribeAudience(record)})";
        }

        return $"{record.Station} - station support ({DescribeAudience(record)})";
    }

    private string BuildTvSupportLabel(BroadcastInventoryRecord record, decimal budget)
    {
        var package = EnumeratePackageCandidates(record.Packages)
            .Select(package => new
            {
                Package = package,
                Normalized = _costNormalizer.NormalizeTvPackage(
                    record.Station,
                    package.Name,
                    package.InvestmentZar,
                    package.PackageCostZar,
                    package.CostPerMonthZar,
                    GetDurationWeeks(package.Name),
                    GetDurationMonths(package.Name))
            })
            .Where(x => x.Normalized.MonthlyCostEstimateZar > 0m)
            .OrderBy(x => Math.Abs(x.Normalized.MonthlyCostEstimateZar - (budget * 0.35m)))
            .FirstOrDefault();

        if (package is not null)
        {
            return $"{record.Station} - {package.Package.Name} ({DescribeAudience(record)})";
        }

        var rate = EnumerateRateCandidates(record.Pricing)
            .Select(rate => new
            {
                Rate = rate,
                Normalized = _costNormalizer.NormalizeTvRate(record.Station, rate.ProgrammeName, rate.SlotLabel, rate.GroupName, rate.RateZar)
            })
            .Where(x => x.Normalized.MonthlyCostEstimateZar > 0m)
            .OrderBy(x => Math.Abs(x.Normalized.MonthlyCostEstimateZar - (budget * 0.22m)))
            .FirstOrDefault();

        if (rate is not null)
        {
            var programme = rate.Rate.ProgrammeName ?? rate.Rate.SlotLabel;
            return $"{record.Station} - {programme} ({DescribeAudience(record)})";
        }

        return $"{record.Station} - channel support ({DescribeAudience(record)})";
    }

    private static string DescribeAudience(BroadcastInventoryRecord record)
    {
        if (!string.IsNullOrWhiteSpace(record.TargetAudience))
        {
            var lowered = record.TargetAudience.Trim().ToLowerInvariant();
            if (lowered.Contains("business")) return "business audience";
            if (lowered.Contains("lifestyle")) return "lifestyle audience";
            if (lowered.Contains("youth")) return "youth audience";
            if (lowered.Contains("community")) return "community audience";
        }

        if (record.AudienceKeywords.Any(keyword => keyword.Contains("news", StringComparison.OrdinalIgnoreCase))) return "news audience";
        if (record.AudienceKeywords.Any(keyword => keyword.Contains("sport", StringComparison.OrdinalIgnoreCase))) return "sport audience";
        if (record.AudienceKeywords.Any(keyword => keyword.Contains("lifestyle", StringComparison.OrdinalIgnoreCase))) return "lifestyle audience";
        return "selected audience fit";
    }

    private static IEnumerable<BroadcastPackageCandidate> EnumeratePackageCandidates(JsonElement packages)
    {
        if (packages.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        foreach (var item in packages.EnumerateArray())
        {
            var candidate = new BroadcastPackageCandidate
            {
                Name = GetString(item, "name") ?? "Package",
                InvestmentZar = GetDecimal(item, "investment_zar"),
                PackageCostZar = GetDecimal(item, "package_cost_zar"),
                CostPerMonthZar = GetDecimal(item, "cost_per_month_zar"),
                Exposure = GetInt(item, "exposure"),
                TotalExposure = GetInt(item, "total_exposure"),
                NumberOfSpots = GetInt(item, "number_of_spots"),
                Notes = GetString(item, "notes")
            };

            if (item.TryGetProperty("elements", out var elements) && elements.ValueKind == JsonValueKind.Array)
            {
                foreach (var element in elements.EnumerateArray())
                {
                    yield return new BroadcastPackageCandidate
                    {
                        Name = $"{candidate.Name} - {GetString(element, "name") ?? "Element"}",
                        InvestmentZar = GetDecimal(element, "investment_zar"),
                        PackageCostZar = GetDecimal(element, "package_cost_zar"),
                        CostPerMonthZar = candidate.CostPerMonthZar,
                        Exposure = GetInt(element, "exposure"),
                        TotalExposure = GetInt(element, "total_exposure"),
                        NumberOfSpots = GetInt(element, "number_of_spots"),
                        Notes = GetString(element, "notes")
                    };
                }

                continue;
            }

            yield return candidate;
        }
    }

    private static IEnumerable<BroadcastRateCandidate> EnumerateRateCandidates(JsonElement pricing)
    {
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
                        JsonValueKind.Number when slot.Value.TryGetDecimal(out var numberRate) => numberRate,
                        JsonValueKind.String when decimal.TryParse(slot.Value.GetString(), out var stringRate) => stringRate,
                        _ => 0m
                    };

                    if (rate <= 0m)
                    {
                        continue;
                    }

                    yield return new BroadcastRateCandidate
                    {
                        GroupName = group.Name,
                        SlotLabel = slot.Name,
                        RateZar = rate
                    };
                }
            }
        }
        else if (pricing.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in pricing.EnumerateArray())
            {
                var rate = GetDecimal(item, "price_zar") ?? GetDecimal(item, "rate_zar") ?? 0m;
                if (rate <= 0m)
                {
                    continue;
                }

                yield return new BroadcastRateCandidate
                {
                    GroupName = GetString(item, "group") ?? "schedule",
                    SlotLabel = GetString(item, "slot") ?? GetString(item, "time") ?? "selected slot",
                    RateZar = rate,
                    ProgrammeName = GetString(item, "program") ?? GetString(item, "programme")
                };
            }
        }
    }

    private static string HumanizeDaypart(string? daypart)
    {
        if (string.IsNullOrWhiteSpace(daypart))
        {
            return "selected market";
        }

        return daypart.Trim().ToLowerInvariant() switch
        {
            "breakfast" => "breakfast",
            "drive" => "drive-time",
            "midday" => "midday",
            _ => daypart.Trim().ToLowerInvariant()
        };
    }

    private static string NormalizeBroadcastToken(string value)
    {
        return value.Trim().Replace(" ", "_").Replace("-", "_").ToLowerInvariant();
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

        if (property.ValueKind == JsonValueKind.Number && property.TryGetDecimal(out var number))
        {
            return number;
        }

        if (property.ValueKind == JsonValueKind.String
            && decimal.TryParse(property.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed))
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

        if (property.ValueKind == JsonValueKind.String
            && int.TryParse(property.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        return null;
    }

    private static int? GetDurationMonths(string? name)
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

    private static int? GetDurationWeeks(string? name)
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
