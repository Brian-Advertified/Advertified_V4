using Advertified.App.Services.Abstractions;

namespace Advertified.App.Services;

public sealed class PackagePreviewFormatter : IPackagePreviewFormatter
{
    public IReadOnlyList<string> BuildExampleLocations(IReadOnlyList<OohPreviewRow> rows, PackagePreviewAreaProfile selectedArea)
    {
        if (rows.Count == 0)
        {
            return selectedArea.FallbackExampleLocations.Count > 0
                ? selectedArea.FallbackExampleLocations
                : new List<string>
                {
                    "Top commuter corridors",
                    "Retail-led urban nodes",
                    "High-traffic regional routes"
                };
        }

        return rows
            .Select(row => new
            {
                Label = BuildExampleLocationLabel(row),
                row.TrafficCount
            })
            .GroupBy(x => x.Label, StringComparer.OrdinalIgnoreCase)
            .Select(group => group
                .OrderByDescending(x => x.TrafficCount)
                .First()
                .Label)
            .Take(3)
            .ToList();
    }

    public string GetCoverageLabel(string bandCode, decimal budget, decimal minBudget, decimal maxBudget)
    {
        var ratio = maxBudget <= minBudget ? 0m : (budget - minBudget) / (maxBudget - minBudget);
        var normalizedCode = bandCode.Trim().ToLowerInvariant();

        return normalizedCode switch
        {
            "launch" => ratio < 0.65m ? "Single area -> focused local coverage" : "Local -> multi-area starter coverage",
            "boost" => ratio < 0.65m ? "Local -> regional coverage" : "Regional -> selected multi-area coverage",
            "scale" => ratio < 0.65m ? "Regional -> broader market coverage" : "Broad regional -> multi-zone coverage",
            _ => ratio < 0.65m ? "Regional -> multi-area coverage" : "Broad regional -> national-scale options"
        };
    }

    public IReadOnlyList<string> BuildMediaMix(string bandCode, decimal budget)
    {
        var normalizedCode = bandCode.Trim().ToLowerInvariant();

        return normalizedCode switch
        {
            "launch" => budget <= 65000m
                ? new List<string> { "1-2 local Billboards & Digital Screens or digital placements", "Starter radio support", "Single-area visibility" }
                : new List<string> { "1-2 stronger local placements", "Small mixed-media route", "More concentrated local reach" },
            "boost" => budget <= 250000m
                ? new List<string> { "2-3 media items", "Billboards & Digital Screens footprint plus radio support", "Selected multi-area coverage" }
                : new List<string> { "2-4 media items", "Stronger Billboards & Digital Screens and radio balance", "Broader regional visibility" },
            "scale" => budget <= 750000m
                ? new List<string> { "Multi-channel media mix", "Balanced radio plus Billboards & Digital Screens support", "Regional campaign coverage" }
                : new List<string> { "Stronger multi-channel plan", "Higher frequency support", "Broader target-zone coverage" },
            _ => budget <= 1600000m
                ? new List<string> { "Premium Billboards & Digital Screens placements", "Radio support in key markets", "Broader regional reach" }
                : new List<string> { "Premium Billboards & Digital Screens, radio, and selected digital placements", "Multi-region or national-scale options", "Higher-frequency exposure" }
        };
    }

    private static string BuildExampleLocationLabel(OohPreviewRow row)
    {
        var areaLabel = row.Suburb.Equals(row.City, StringComparison.OrdinalIgnoreCase)
            ? row.Suburb
            : $"{row.Suburb}, {row.City}";

        var audienceCue = row.TrafficCount switch
        {
            >= 3000000 => "high-income commuter traffic",
            >= 1200000 => "strong retail and commuter movement",
            >= 600000 => "high foot and vehicle traffic",
            _ => "consistent local visibility"
        };

        return $"{areaLabel} ({audienceCue})";
    }
}
