using System.Text.Json;
using Advertified.App.Data.Entities;
using Advertified.App.Services;

namespace Advertified.App.Support;

public static class RecommendationAuditSupport
{
    private static readonly JsonSerializerOptions AuditJsonOptions = new(JsonSerializerDefaults.Web);
    public static RecommendationFallbackState ResolveFallbackState(CampaignRecommendation? recommendation)
    {
        if (recommendation is null)
        {
            return new RecommendationFallbackState(false, Array.Empty<string>());
        }

        var latestAudit = recommendation.RecommendationRunAudits
            .OrderByDescending(entry => entry.CreatedAt)
            .FirstOrDefault();
        if (latestAudit is not null)
        {
            var snapshot = Deserialize<RecommendationFallbackAuditSnapshot>(latestAudit.FallbackFlagsJson);
            var flags = snapshot?.FallbackFlags
                ?.Where(static value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray()
                ?? Array.Empty<string>();
            return new RecommendationFallbackState(latestAudit.ManualReviewRequired, flags);
        }

        return new RecommendationFallbackState(false, Array.Empty<string>());
    }

    private static T? Deserialize<T>(string? json)
    {
        return string.IsNullOrWhiteSpace(json)
            ? default
            : JsonSerializer.Deserialize<T>(json, AuditJsonOptions);
    }
    private sealed class RecommendationFallbackAuditSnapshot
    {
        public List<string> FallbackFlags { get; set; } = new();
    }
}
