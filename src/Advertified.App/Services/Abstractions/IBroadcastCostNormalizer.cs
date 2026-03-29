using System.Text.Json;

namespace Advertified.App.Services.Abstractions;

public interface IBroadcastCostNormalizer
{
    NormalizedCostResult NormalizeRadioPackage(
        string station,
        string? packageName,
        decimal? investmentZar,
        decimal? packageCostZar,
        decimal? costPerMonthZar,
        int? durationMonths);

    NormalizedCostResult NormalizeRadioRate(
        string station,
        string? slotLabel,
        string? groupName,
        decimal rawRateZar,
        RadioNormalizationContext? context = null);

    NormalizedCostResult NormalizeTvPackage(
        string station,
        string? packageName,
        decimal? investmentZar,
        decimal? packageCostZar,
        decimal? costPerMonthZar,
        int? durationWeeks,
        int? durationMonths);

    NormalizedCostResult NormalizeTvRate(
        string station,
        string? programmeName,
        string? slotLabel,
        string? groupName,
        decimal rawRateZar,
        TvNormalizationContext? context = null);

    decimal? GetClosestMonthlySpendPoint(
        string mediaType,
        string station,
        JsonElement packages,
        JsonElement pricing);
}

public sealed record NormalizedCostResult(
    decimal RawCostZar,
    decimal MonthlyCostEstimateZar,
    string CostType,
    string NormalizationNote)
{
    public static NormalizedCostResult Empty(string costType)
        => new(0m, 0m, costType, "No usable pricing.");
}

public sealed record RadioNormalizationContext(
    int SpotsPerDay,
    int ActiveDaysPerMonth,
    decimal DaypartWeight = 1.0m);

public sealed record TvNormalizationContext(
    int InsertionsPerMonth,
    string? CostType = null);

public sealed record RadioNormalizationProfile(
    int SpotsPerDay,
    int ActiveDaysPerMonth,
    decimal DaypartWeight);

public sealed record TvNormalizationProfile(
    int InsertionsPerMonth,
    string CostType);

public sealed class BroadcastNormalizationOptions
{
    public RadioNormalizationProfile RadioDefaultProfile { get; init; } = new(1, 20, 1.0m);
    public RadioNormalizationProfile RadioRegionalProfile { get; init; } = new(2, 20, 1.0m);
    public RadioNormalizationProfile RadioPremiumProfile { get; init; } = new(1, 20, 1.0m);

    public TvNormalizationProfile TvDefaultProfile { get; init; } = new(4, "tv_slot");
    public TvNormalizationProfile TvPrimeProfile { get; init; } = new(4, "tv_prime_slot");

    public static BroadcastNormalizationOptions Default() => new();
}

