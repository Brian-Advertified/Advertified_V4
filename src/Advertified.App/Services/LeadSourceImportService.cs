using System.Text;
using Advertified.App.Contracts.Leads;
using Advertified.App.Services.Abstractions;

namespace Advertified.App.Services;

public sealed class LeadSourceImportService : ILeadSourceImportService
{
    private static readonly string[][] StandardNameKeys = new[]
    {
        new[] { "name" },
        new[] { "businessname" },
        new[] { "title" }
    };

    private static readonly string[][] StandardWebsiteKeys = new[]
    {
        new[] { "website" },
        new[] { "site" },
        new[] { "domain" }
    };

    private static readonly string[][] StandardLocationKeys = new[]
    {
        new[] { "location" },
        new[] { "city" },
        new[] { "address" }
    };

    private static readonly string[][] StandardCategoryKeys = new[]
    {
        new[] { "category" },
        new[] { "maincategory" },
        new[] { "primarycategory" }
    };

    private static readonly string[][] StandardSourceKeys = new[]
    {
        new[] { "source" }
    };

    private static readonly string[][] StandardSourceReferenceKeys = new[]
    {
        new[] { "source_reference" },
        new[] { "sourcereference" },
        new[] { "reference" },
        new[] { "placeid" },
        new[] { "businessid" }
    };

    private readonly ILeadSourceIngestionService _leadSourceIngestionService;

    public LeadSourceImportService(ILeadSourceIngestionService leadSourceIngestionService)
    {
        _leadSourceIngestionService = leadSourceIngestionService;
    }

    public Task<LeadSourceIngestionResult> ImportCsvAsync(
        string csvText,
        string defaultSource,
        string importProfile,
        CancellationToken cancellationToken)
    {
        var rows = ParseCsv(csvText);
        if (rows.Count == 0)
        {
            return Task.FromResult(new LeadSourceIngestionResult());
        }

        var headers = rows[0]
            .Select((value, index) => new { value, index })
            .ToDictionary(
                item => NormalizeHeader(item.value),
                item => item.index,
                StringComparer.OrdinalIgnoreCase);

        var profile = ResolveProfile(importProfile, defaultSource, headers);
        var leads = new List<IngestLeadSourceItemRequest>();

        foreach (var row in rows.Skip(1))
        {
            if (row.All(string.IsNullOrWhiteSpace))
            {
                continue;
            }

            var mappedLead = MapLeadRow(row, headers, profile, defaultSource);
            if (mappedLead is not null)
            {
                leads.Add(mappedLead);
            }
        }

        return _leadSourceIngestionService.IngestAsync(leads, cancellationToken);
    }

    private static IngestLeadSourceItemRequest? MapLeadRow(
        IReadOnlyList<string> row,
        IReadOnlyDictionary<string, int> headers,
        string profile,
        string defaultSource)
    {
        var name = GetFirstValue(row, headers, StandardNameKeys);
        var website = GetFirstValue(row, headers, StandardWebsiteKeys);
        var location = profile == "google_maps"
            ? GetGoogleMapsLocation(row, headers)
            : GetFirstValue(row, headers, StandardLocationKeys);
        var category = profile == "google_maps"
            ? GetGoogleMapsCategory(row, headers)
            : GetFirstValue(row, headers, StandardCategoryKeys);
        var source = GetFirstValue(row, headers, StandardSourceKeys);
        var sourceReference = profile == "google_maps"
            ? GetGoogleMapsSourceReference(row, headers)
            : GetFirstValue(row, headers, StandardSourceReferenceKeys);

        if (string.IsNullOrWhiteSpace(name) ||
            string.IsNullOrWhiteSpace(location) ||
            string.IsNullOrWhiteSpace(category))
        {
            return null;
        }

        var resolvedDefaultSource = string.IsNullOrWhiteSpace(defaultSource)
            ? (profile == "google_maps" ? "google_maps" : "csv_import")
            : defaultSource.Trim();

        return new IngestLeadSourceItemRequest
        {
            Name = name,
            Website = website,
            Location = location,
            Category = category,
            Source = string.IsNullOrWhiteSpace(source) ? resolvedDefaultSource : source,
            SourceReference = sourceReference,
        };
    }

    private static string ResolveProfile(
        string importProfile,
        string defaultSource,
        IReadOnlyDictionary<string, int> headers)
    {
        var normalizedProfile = importProfile?.Trim().ToLowerInvariant();
        if (normalizedProfile is "google_maps" or "standard")
        {
            return normalizedProfile;
        }

        if (string.Equals(defaultSource?.Trim(), "google_maps", StringComparison.OrdinalIgnoreCase))
        {
            return "google_maps";
        }

        if (headers.ContainsKey("placeid") || headers.ContainsKey("maincategory"))
        {
            return "google_maps";
        }

        return "standard";
    }

    private static string? GetGoogleMapsLocation(
        IReadOnlyList<string> row,
        IReadOnlyDictionary<string, int> headers)
    {
        return GetFirstValue(row, headers,
            new[] { "city" },
            new[] { "location" },
            new[] { "address" });
    }

    private static string? GetGoogleMapsCategory(
        IReadOnlyList<string> row,
        IReadOnlyDictionary<string, int> headers)
    {
        return GetFirstValue(row, headers,
            new[] { "maincategory" },
            new[] { "primarycategory" },
            new[] { "category" },
            new[] { "categories" });
    }

    private static string? GetGoogleMapsSourceReference(
        IReadOnlyList<string> row,
        IReadOnlyDictionary<string, int> headers)
    {
        return GetFirstValue(row, headers,
            new[] { "placeid" },
            new[] { "source_reference" },
            new[] { "sourcereference" },
            new[] { "reference" },
            new[] { "businessid" });
    }

    private static string? GetFirstValue(
        IReadOnlyList<string> row,
        IReadOnlyDictionary<string, int> headers,
        params string[][] keys)
    {
        foreach (var keySet in keys)
        {
            foreach (var key in keySet)
            {
                var value = GetValue(row, headers, key);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }
        }

        return null;
    }

    private static string? GetValue(IReadOnlyList<string> row, IReadOnlyDictionary<string, int> headers, string key)
    {
        if (!headers.TryGetValue(key, out var index))
        {
            return null;
        }

        if (index < 0 || index >= row.Count)
        {
            return null;
        }

        var value = row[index].Trim();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static string NormalizeHeader(string value)
    {
        return value.Trim().Replace(" ", string.Empty).Replace("_", string.Empty).ToLowerInvariant();
    }

    private static List<List<string>> ParseCsv(string csvText)
    {
        var rows = new List<List<string>>();
        var currentRow = new List<string>();
        var currentValue = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < csvText.Length; i++)
        {
            var character = csvText[i];

            if (character == '"')
            {
                if (inQuotes && i + 1 < csvText.Length && csvText[i + 1] == '"')
                {
                    currentValue.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }

                continue;
            }

            if (!inQuotes && character == ',')
            {
                currentRow.Add(currentValue.ToString());
                currentValue.Clear();
                continue;
            }

            if (!inQuotes && (character == '\r' || character == '\n'))
            {
                if (character == '\r' && i + 1 < csvText.Length && csvText[i + 1] == '\n')
                {
                    i++;
                }

                currentRow.Add(currentValue.ToString());
                currentValue.Clear();

                rows.Add(currentRow);
                currentRow = new List<string>();
                continue;
            }

            currentValue.Append(character);
        }

        if (currentValue.Length > 0 || currentRow.Count > 0)
        {
            currentRow.Add(currentValue.ToString());
            rows.Add(currentRow);
        }

        return rows;
    }
}
