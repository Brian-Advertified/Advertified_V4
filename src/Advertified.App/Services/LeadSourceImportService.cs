using System.Text;
using Advertified.App.Contracts.Leads;
using Advertified.App.Services.Abstractions;

namespace Advertified.App.Services;

public sealed class LeadSourceImportService : ILeadSourceImportService
{
    private readonly ILeadSourceIngestionService _leadSourceIngestionService;

    public LeadSourceImportService(ILeadSourceIngestionService leadSourceIngestionService)
    {
        _leadSourceIngestionService = leadSourceIngestionService;
    }

    public Task<LeadSourceIngestionResult> ImportCsvAsync(
        string csvText,
        string defaultSource,
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

        var leads = new List<IngestLeadSourceItemRequest>();
        foreach (var row in rows.Skip(1))
        {
            if (row.All(string.IsNullOrWhiteSpace))
            {
                continue;
            }

            var name = GetValue(row, headers, "name");
            var website = GetValue(row, headers, "website");
            var location = GetValue(row, headers, "location");
            var category = GetValue(row, headers, "category");
            var source = GetValue(row, headers, "source") ?? defaultSource;
            var sourceReference = GetValue(row, headers, "source_reference")
                ?? GetValue(row, headers, "sourcereference")
                ?? GetValue(row, headers, "reference");

            if (string.IsNullOrWhiteSpace(name) ||
                string.IsNullOrWhiteSpace(location) ||
                string.IsNullOrWhiteSpace(category))
            {
                continue;
            }

            leads.Add(new IngestLeadSourceItemRequest
            {
                Name = name,
                Website = website,
                Location = location,
                Category = category,
                Source = string.IsNullOrWhiteSpace(source) ? defaultSource : source,
                SourceReference = sourceReference,
            });
        }

        return _leadSourceIngestionService.IngestAsync(leads, cancellationToken);
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
