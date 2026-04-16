using Advertified.App.Domain.Campaigns;
using Advertified.App.Services;
using System.Text.Json;

namespace Advertified.App.Support;

internal static class BroadcastLanguageSupport
{
    internal static IReadOnlyList<string> NormalizeRequestedLanguages(
        IEnumerable<string> languages,
        Func<string?, string> normalizeLanguage)
    {
        return languages
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(normalizeLanguage)
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    internal static IReadOnlyList<string> ExtractCandidateLanguageCodes(
        InventoryCandidate candidate,
        Func<string?, string> normalizeLanguage)
    {
        var values = new List<string>();
        values.AddRange(ReadMetadataLanguages(candidate.Metadata, "primaryLanguages", "primary_languages"));
        values.AddRange(ReadMetadataLanguages(candidate.Metadata, "secondaryLanguage", "secondary_language"));

        if (!string.IsNullOrWhiteSpace(candidate.Language))
        {
            values.Add(candidate.Language);
        }

        return values
            .Select(normalizeLanguage)
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    internal static IReadOnlyList<string> ExtractPlannedItemLanguageCodes(
        PlannedItem item,
        Func<string?, string> normalizeLanguage)
    {
        var values = new List<string>();
        values.AddRange(ReadMetadataLanguages(item.Metadata, "primaryLanguages", "primary_languages"));
        values.AddRange(ReadMetadataLanguages(item.Metadata, "secondaryLanguage", "secondary_language"));

        if (item.Metadata.TryGetValue("language", out var languageValue))
        {
            values.AddRange(FlattenMetadataValue(languageValue));
        }

        return values
            .Select(normalizeLanguage)
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IEnumerable<string> ReadMetadataLanguages(
        IReadOnlyDictionary<string, object?> metadata,
        params string[] keys)
    {
        foreach (var key in keys)
        {
            if (metadata.TryGetValue(key, out var value))
            {
                foreach (var token in FlattenMetadataValue(value))
                {
                    yield return token;
                }
            }
        }
    }

    private static IEnumerable<string> FlattenMetadataValue(object? value)
    {
        if (value is null)
        {
            yield break;
        }

        if (value is string text)
        {
            foreach (var token in SplitLanguageText(text))
            {
                yield return token;
            }

            yield break;
        }

        if (value is IEnumerable<string> stringValues)
        {
            foreach (var entry in stringValues)
            {
                foreach (var token in SplitLanguageText(entry))
                {
                    yield return token;
                }
            }

            yield break;
        }

        if (value is JsonElement json)
        {
            if (json.ValueKind == JsonValueKind.String)
            {
                foreach (var token in SplitLanguageText(json.GetString()))
                {
                    yield return token;
                }

                yield break;
            }

            if (json.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in json.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String)
                    {
                        foreach (var token in SplitLanguageText(item.GetString()))
                        {
                            yield return token;
                        }
                    }
                }

                yield break;
            }
        }

        foreach (var token in SplitLanguageText(value.ToString()))
        {
            yield return token;
        }
    }

    private static IEnumerable<string> SplitLanguageText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            yield break;
        }

        foreach (var token in value.Split(new[] { '/', ',', ';', '|', '&' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!string.IsNullOrWhiteSpace(token))
            {
                yield return token;
            }
        }
    }
}

