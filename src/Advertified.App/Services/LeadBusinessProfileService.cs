using Advertified.App.Data.Entities;
using Advertified.App.Services.Abstractions;

namespace Advertified.App.Services;

public sealed class LeadBusinessProfileService : ILeadBusinessProfileService
{
    public LeadBusinessProfile Build(
        Lead lead,
        LeadEnrichmentSnapshot enrichmentSnapshot,
        LeadIndustryPolicyProfile industryPolicy,
        LeadOpportunityProfile opportunityProfile)
    {
        var fieldsByKey = enrichmentSnapshot.Fields.ToDictionary(field => field.Key, StringComparer.OrdinalIgnoreCase);
        var languageValues = ResolveLanguages(fieldsByKey);
        var businessType = ResolveValue(fieldsByKey, "industry", lead.Category, fallback: industryPolicy.Name);
        var location = ResolveValue(fieldsByKey, "location", lead.Location, fallback: "South Africa");
        var audience = ResolveValue(
            fieldsByKey,
            "target_audience",
            fallback: opportunityProfile.Name.Equals("Invisible Local Business", StringComparison.OrdinalIgnoreCase)
                ? "Local intent-driven audience"
                : "Growth-focused local buyers");
        var gender = ResolveValue(fieldsByKey, "gender", fallback: "Broad");

        return new LeadBusinessProfile
        {
            BusinessType = businessType,
            PrimaryLocation = location,
            TargetAudience = audience,
            GenderFocus = gender,
            Languages = languageValues,
            ConfidenceScore = enrichmentSnapshot.ConfidenceScore,
            MissingFields = enrichmentSnapshot.MissingFields,
            EvidenceTrace = enrichmentSnapshot.Fields
                .Select(field => new LeadEvidenceFieldTrace
                {
                    Field = field.Label,
                    Value = string.IsNullOrWhiteSpace(field.Value) ? "Not detected" : field.Value,
                    Confidence = field.Confidence,
                    Source = field.Source,
                    Reason = field.Reason
                })
                .ToArray()
        };
    }

    private static IReadOnlyList<string> ResolveLanguages(IReadOnlyDictionary<string, LeadEnrichmentField> fieldsByKey)
    {
        var values = new[]
            {
                ResolveValue(fieldsByKey, "language"),
                ResolveValue(fieldsByKey, "secondary_language")
            }
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToArray();

        if (values.Length == 0)
        {
            return new[] { "English" };
        }

        return values
            .SelectMany(value => value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Select(value => value.Equals("English-primary", StringComparison.OrdinalIgnoreCase) ? "English" : value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string ResolveValue(
        IReadOnlyDictionary<string, LeadEnrichmentField> fieldsByKey,
        string key,
        string? rawFallback = null,
        string? fallback = null)
    {
        if (fieldsByKey.TryGetValue(key, out var field) && !string.IsNullOrWhiteSpace(field.Value))
        {
            return field.Value.Trim();
        }

        if (!string.IsNullOrWhiteSpace(rawFallback))
        {
            return rawFallback.Trim();
        }

        return fallback ?? string.Empty;
    }
}
