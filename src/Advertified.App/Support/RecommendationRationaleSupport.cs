namespace Advertified.App.Support;

internal static class RecommendationRationaleSupport
{
    private const string ClientFeedbackMarker = "Client feedback:";
    private const string FallbackFlagsMarker = "Fallback flags:";
    private const string ManualReviewMarker = "Manual review required:";

    internal static string RemoveInternalMarkers(string? rationale)
    {
        if (string.IsNullOrWhiteSpace(rationale))
        {
            return string.Empty;
        }

        var cleanedLines = rationale
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Where(line =>
                !line.StartsWith(ClientFeedbackMarker, StringComparison.OrdinalIgnoreCase)
                && !line.StartsWith(FallbackFlagsMarker, StringComparison.OrdinalIgnoreCase)
                && !line.StartsWith(ManualReviewMarker, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        return cleanedLines.Length == 0
            ? string.Empty
            : string.Join(Environment.NewLine, cleanedLines);
    }
}
