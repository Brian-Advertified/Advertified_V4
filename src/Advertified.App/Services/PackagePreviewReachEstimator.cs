using Advertified.App.Services.Abstractions;

namespace Advertified.App.Services;

public sealed class PackagePreviewReachEstimator : IPackagePreviewReachEstimator
{
    public string Estimate(string bandCode, decimal budgetRatio, int oohCount, int radioCount)
    {
        var normalizedBandCode = bandCode.Trim().ToLowerInvariant();
        var totalSupports = Math.Max(1, oohCount + radioCount);

        var (minImpressions, maxImpressions) = normalizedBandCode switch
        {
            "launch" => (70_000, 180_000),
            "boost" => (180_000, 420_000),
            "scale" => (500_000, 1_200_000),
            _ => (1_500_000, 4_000_000)
        };

        var ratioMultiplier = 0.8m + (budgetRatio * 0.6m);
        var supportMultiplier = 0.9m + (Math.Min(totalSupports, 6) * 0.08m);

        var adjustedMin = (int)Math.Round(minImpressions * ratioMultiplier * supportMultiplier, MidpointRounding.AwayFromZero);
        var adjustedMax = (int)Math.Round(maxImpressions * ratioMultiplier * supportMultiplier, MidpointRounding.AwayFromZero);

        return $"~{FormatImpressions(adjustedMin)} - {FormatImpressions(adjustedMax)} impressions";
    }

    private static string FormatImpressions(int value)
    {
        return value switch
        {
            >= 1_000_000 => $"{Math.Round(value / 1_000_000m, 1):0.#}M",
            >= 1_000 => $"{Math.Round(value / 1_000m, 0):0}K",
            _ => value.ToString("0")
        };
    }
}
