using System.Text.Json;
using System.Text.Json.Serialization;
using Advertified.App.Contracts.Admin;

namespace Advertified.App.Tests;

public class BroadcastInventoryDataTests
{
    [Fact]
    public void NormalizedBroadcastInventory_UsesExpectedHealthForCuratedStations()
    {
        var records = LoadRecords();

        var expectedStrongStations = new[]
        {
            "Algoa FM",
            "Good Hope FM",
            "Jozi FM",
            "Lesedi FM",
            "Ligwalagwala FM",
            "Lotus FM",
            "Metro FM",
            "Motsweding FM",
            "Munghana Lonene FM",
            "Phalaphala FM",
            "XK FM",
            "Channel Africa"
        };

        foreach (var station in expectedStrongStations)
        {
            var record = records.Single(x => string.Equals(x.Station, station, StringComparison.Ordinal));
            Assert.Equal("strong", record.CatalogHealth);
            Assert.True(record.HasPricing);
            Assert.True(record.Packages.GetArrayLength() > 0 || CountPricingEntries(record.Pricing) > 0);
        }
    }

    [Fact]
    public void ChannelAfrica_UsesNationalGeography()
    {
        var records = LoadRecords();

        var record = records.Single(x => string.Equals(x.Station, "Channel Africa", StringComparison.Ordinal));

        Assert.Contains("national", record.ProvinceCodes, StringComparer.OrdinalIgnoreCase);
    }

    private static BroadcastInventoryRecord[] LoadRecords()
    {
        var path = ResolveRepoPath("src", "Advertified.App", "App_Data", "broadcast", "enriched_broadcast_inventory_normalized.json");
        using var stream = File.OpenRead(path);
        using var document = JsonDocument.Parse(stream);
        return document.RootElement.GetProperty("records").Deserialize<BroadcastInventoryRecord[]>(new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? Array.Empty<BroadcastInventoryRecord>();
    }

    private static string ResolveRepoPath(params string[] parts)
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, Path.Combine(parts));
            if (File.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        throw new FileNotFoundException("Could not resolve repository file path.", Path.Combine(parts));
    }

    private static int CountPricingEntries(JsonElement pricing)
    {
        if (pricing.ValueKind == JsonValueKind.Array)
        {
            return pricing.GetArrayLength();
        }

        if (pricing.ValueKind != JsonValueKind.Object)
        {
            return 0;
        }

        var count = 0;
        foreach (var day in pricing.EnumerateObject())
        {
            count += day.Value.ValueKind switch
            {
                JsonValueKind.Array => day.Value.GetArrayLength(),
                JsonValueKind.Object => day.Value.EnumerateObject().Count(),
                _ => 0
            };
        }

        return count;
    }

    private sealed class BroadcastInventoryRecord
    {
        [JsonPropertyName("station")]
        public string Station { get; set; } = string.Empty;

        [JsonPropertyName("catalog_health")]
        public string CatalogHealth { get; set; } = string.Empty;

        [JsonPropertyName("has_pricing")]
        public bool HasPricing { get; set; }

        [JsonPropertyName("province_codes")]
        public List<string> ProvinceCodes { get; set; } = new();

        [JsonPropertyName("packages")]
        public JsonElement Packages { get; set; }

        [JsonPropertyName("pricing")]
        public JsonElement Pricing { get; set; }
    }
}
