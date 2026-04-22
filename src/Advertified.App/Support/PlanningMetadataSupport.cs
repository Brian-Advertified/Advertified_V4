using Advertified.App.Domain.Campaigns;
using System.Text.Json;

namespace Advertified.App.Support;

internal static class PlanningMetadataSupport
{
    public static string[] ExtractMetadataTokens(object? value)
    {
        if (value is null)
        {
            return Array.Empty<string>();
        }

        if (value is string text)
        {
            return string.IsNullOrWhiteSpace(text) ? Array.Empty<string>() : new[] { text.Trim() };
        }

        if (value is IEnumerable<string> textValues)
        {
            return textValues
                .Where(static entry => !string.IsNullOrWhiteSpace(entry))
                .Select(static entry => entry.Trim())
                .ToArray();
        }

        if (value is JsonElement json)
        {
            if (json.ValueKind == JsonValueKind.String)
            {
                var jsonText = json.GetString();
                return string.IsNullOrWhiteSpace(jsonText) ? Array.Empty<string>() : new[] { jsonText.Trim() };
            }

            if (json.ValueKind == JsonValueKind.Array)
            {
                return json
                    .EnumerateArray()
                    .Where(static item => item.ValueKind == JsonValueKind.String)
                    .Select(static item => item.GetString())
                    .Where(static item => !string.IsNullOrWhiteSpace(item))
                    .Select(static item => item!.Trim())
                    .ToArray();
            }

            return Array.Empty<string>();
        }

        var fallback = value.ToString();
        return string.IsNullOrWhiteSpace(fallback) ? Array.Empty<string>() : new[] { fallback.Trim() };
    }

    public static string[] ExtractRequestTokens(IEnumerable<string> values)
    {
        return values
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .SelectMany(value => value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static bool MatchesAnyMetadataToken(InventoryCandidate candidate, Func<string, bool> predicate, params string[] keys)
    {
        return candidate.Metadata.Count > 0
            && keys.Any(key =>
                candidate.Metadata.TryGetValue(key, out var value)
                && ExtractMetadataTokens(value).Any(predicate));
    }

    public static bool HasBroadcastGeoTokenMatch(InventoryCandidate candidate, IEnumerable<string> requestedValues, params string[] keys)
    {
        if ((!candidate.MediaType.Equals("Radio", StringComparison.OrdinalIgnoreCase)
                && !candidate.MediaType.Equals("TV", StringComparison.OrdinalIgnoreCase))
            || candidate.Metadata.Count == 0)
        {
            return false;
        }

        var tokens = ExtractRequestTokens(requestedValues);
        return tokens.Length > 0
            && MatchesAnyMetadataToken(candidate, token => tokens.Contains(token, StringComparer.OrdinalIgnoreCase), keys);
    }

    public static bool MatchesStrategyMetadataToken(InventoryCandidate candidate, string requestedValue, params string[] keys)
    {
        return MatchesAnyMetadataToken(candidate, token => MatchesStrategyToken(requestedValue, token), keys);
    }

    public static string NormalizeStrategyToken(string value)
    {
        return value
            .Trim()
            .ToLowerInvariant()
            .Replace('|', ' ')
            .Replace('/', ' ')
            .Replace('-', '_')
            .Replace(' ', '_');
    }

    public static bool MatchesStrategyToken(string requestedValue, string metadataToken)
    {
        var normalizedRequested = NormalizeStrategyToken(requestedValue);
        var normalizedMetadata = NormalizeStrategyToken(metadataToken);
        if (normalizedRequested.Length == 0 || normalizedMetadata.Length == 0)
        {
            return false;
        }

        return normalizedRequested == normalizedMetadata
            || normalizedMetadata.Contains(normalizedRequested, StringComparison.OrdinalIgnoreCase)
            || normalizedRequested.Contains(normalizedMetadata, StringComparison.OrdinalIgnoreCase);
    }
}
