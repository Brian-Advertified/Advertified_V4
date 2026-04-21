using Advertified.App.Data.Entities;
using Advertified.App.Services.Abstractions;
using Advertified.App.Support;

namespace Advertified.App.Services;

public sealed class LeadEnrichmentSnapshotService : ILeadEnrichmentSnapshotService
{
    private readonly IGeocodingService _geocodingService;
    private readonly ILeadMasterDataService _leadMasterDataService;

    public LeadEnrichmentSnapshotService(IGeocodingService geocodingService, ILeadMasterDataService leadMasterDataService)
    {
        _geocodingService = geocodingService;
        _leadMasterDataService = leadMasterDataService;
    }

    public LeadEnrichmentSnapshotService(IGeocodingService geocodingService)
        : this(geocodingService, new NoOpLeadMasterDataService())
    {
    }

    public LeadEnrichmentSnapshotService()
        : this(new NoOpGeocodingService(), new NoOpLeadMasterDataService())
    {
    }

    public LeadEnrichmentSnapshot Build(
        Lead lead,
        Signal? latestSignal,
        IReadOnlyList<LeadSignalEvidence> evidences,
        IReadOnlyList<LeadChannelDetectionResult> channelDetections,
        MasterIndustryMatch? canonicalIndustry = null,
        LeadIndustryContext? industryContext = null)
    {
        var fields = new List<LeadEnrichmentField>
        {
            BuildLocationField(lead, evidences),
            BuildIndustryField(lead, evidences),
            BuildChannelActivityField(channelDetections),
            BuildLanguageField(lead, evidences),
            BuildSecondaryLanguageField(evidences),
            BuildAudienceField(lead, evidences, canonicalIndustry, industryContext),
            BuildGenderField(lead, evidences),
            BuildBudgetTierField(lead, channelDetections, latestSignal),
        };

        var missingRequired = fields
            .Where(field => field.Required && field.Confidence.Equals("unknown", StringComparison.OrdinalIgnoreCase))
            .Select(field => field.Label)
            .ToArray();
        var missingFields = fields
            .Where(field => field.Confidence.Equals("unknown", StringComparison.OrdinalIgnoreCase))
            .Select(field => field.Label)
            .ToArray();
        var isBlocked = missingRequired.Length > 0;
        var confidenceScore = CalculateConfidenceScore(fields);

        return new LeadEnrichmentSnapshot
        {
            Fields = fields,
            ConfidenceGate = new LeadConfidenceGate
            {
                IsBlocked = isBlocked,
                RequiredFields = fields.Where(field => field.Required).Select(field => field.Label).ToArray(),
                MissingRequiredFields = missingRequired,
                Message = isBlocked
                    ? $"Recommendation halted. Missing required confidence fields: {string.Join(", ", missingRequired)}."
                    : "Confidence gate passed. Required fields are present."
            },
            ConfidenceScore = confidenceScore,
            MissingFields = missingFields,
            GeneratedAtUtc = DateTime.UtcNow
        };
    }

    private LeadEnrichmentField BuildLocationField(Lead lead, IReadOnlyList<LeadSignalEvidence> evidences)
    {
        var detectedEvidence = FindEvidence(evidences, "google_business_profile_location", "website_contact_location", "website_location_hint");
        if (detectedEvidence is not null)
        {
            var confidence = detectedEvidence.SignalType.Equals("website_location_hint", StringComparison.OrdinalIgnoreCase)
                ? "inferred"
                : "detected";
            var geocoded = _geocodingService.ResolveLocation(detectedEvidence.Value);
            var locationValue = geocoded.IsResolved && !string.IsNullOrWhiteSpace(geocoded.CanonicalLocation)
                ? geocoded.CanonicalLocation
                : detectedEvidence.Value;
            return CreateField(
                key: "location",
                label: "Location",
                value: locationValue,
                confidence: confidence,
                source: detectedEvidence.Source,
                reason: geocoded.IsResolved
                    ? $"Location extracted from enrichment evidence and normalized via geocoding ({geocoded.Source})."
                    : "Location extracted from external enrichment evidence.",
                required: true);
        }

        if (!string.IsNullOrWhiteSpace(lead.Location))
        {
            var geocoded = _geocodingService.ResolveLocation(lead.Location);
            var locationValue = geocoded.IsResolved && !string.IsNullOrWhiteSpace(geocoded.CanonicalLocation)
                ? geocoded.CanonicalLocation
                : lead.Location.Trim();
            return CreateField(
                key: "location",
                label: "Location",
                value: locationValue,
                confidence: geocoded.IsResolved ? "detected" : "inferred",
                source: geocoded.IsResolved ? "geocoding_service" : "lead_input",
                reason: geocoded.IsResolved
                    ? $"Location provided at lead capture and normalized via geocoding ({geocoded.Source})."
                    : "Location provided directly at lead capture.",
                required: true);
        }

        return CreateUnknownField("location", "Location", true);
    }

    private static LeadEnrichmentField BuildIndustryField(Lead lead, IReadOnlyList<LeadSignalEvidence> evidences)
    {
        var detectedEvidence = FindEvidence(evidences, "google_business_profile_category", "website_industry_hint");
        if (detectedEvidence is not null)
        {
            var confidence = detectedEvidence.SignalType.Equals("google_business_profile_category", StringComparison.OrdinalIgnoreCase)
                ? "detected"
                : "inferred";

            return CreateField(
                key: "industry",
                label: "Industry",
                value: detectedEvidence.Value,
                confidence: confidence,
                source: detectedEvidence.Source,
                reason: "Industry resolved from enrichment evidence.",
                required: true);
        }

        if (!string.IsNullOrWhiteSpace(lead.Category))
        {
            return CreateField(
                key: "industry",
                label: "Industry",
                value: lead.Category.Trim(),
                confidence: "detected",
                source: "lead_input",
                reason: "Industry/category provided directly at lead capture.",
                required: true);
        }

        return CreateUnknownField("industry", "Industry", true);
    }

    private static LeadEnrichmentField BuildChannelActivityField(IReadOnlyList<LeadChannelDetectionResult> channelDetections)
    {
        var strongest = channelDetections
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Channel, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();

        if (strongest is null || strongest.Score <= 19)
        {
            return CreateUnknownField("channel_activity", "Channel activity", false);
        }

        var confidence = strongest.Score >= 80 ? "detected" : "inferred";
        var value = $"{FormatChannel(strongest.Channel)} ({strongest.Score}/100)";
        return CreateField(
            key: "channel_activity",
            label: "Channel activity",
            value: value,
            confidence: confidence,
            source: "channel_detection",
            reason: strongest.DominantReason,
            required: false);
    }

    private LeadEnrichmentField BuildLanguageField(Lead lead, IReadOnlyList<LeadSignalEvidence> evidences)
    {
        var detectedLanguages = ResolveDetectedLanguages(evidences);
        if (detectedLanguages.Count > 0)
        {
            var primaryLanguage = detectedLanguages[0];
            return CreateField(
                key: "language",
                label: "Language",
                value: primaryLanguage.Value,
                confidence: "detected",
                source: primaryLanguage.Source,
                reason: "Primary language extracted from public content signals.",
                required: false);
        }

        if (!string.IsNullOrWhiteSpace(lead.Location))
        {
            var geocoded = _geocodingService.ResolveLocation(lead.Location);
            if (geocoded.IsResolved)
            {
                return CreateField(
                    key: "language",
                    label: "Language",
                    value: "English-primary",
                    confidence: "inferred",
                    source: "geocoding_service",
                    reason: $"No explicit language evidence found; inferred from resolved location context ({geocoded.Source}).",
                    required: false);
            }
        }

        return CreateUnknownField("language", "Language", false);
    }

    private LeadEnrichmentField BuildSecondaryLanguageField(IReadOnlyList<LeadSignalEvidence> evidences)
    {
        var detectedLanguages = ResolveDetectedLanguages(evidences);
        if (detectedLanguages.Count > 1)
        {
            var secondaryLanguage = detectedLanguages[1];
            return CreateField(
                key: "secondary_language",
                label: "Secondary language",
                value: secondaryLanguage.Value,
                confidence: "detected",
                source: secondaryLanguage.Source,
                reason: "Secondary language extracted from public content signals.",
                required: false);
        }

        return CreateUnknownField("secondary_language", "Secondary language", false);
    }

    private LeadEnrichmentField BuildAudienceField(
        Lead lead,
        IReadOnlyList<LeadSignalEvidence> evidences,
        MasterIndustryMatch? canonicalIndustry,
        LeadIndustryContext? industryContext)
    {
        var audienceEvidence = FindEvidence(evidences, "website_audience_hint");
        if (audienceEvidence is not null)
        {
            return CreateField(
                key: "target_audience",
                label: "Target audience",
                value: NormalizeAudienceHint(audienceEvidence.Value),
                confidence: "inferred",
                source: audienceEvidence.Source,
                reason: "Audience inferred from website semantic hints.",
                required: false);
        }

        var industryCode = canonicalIndustry?.Code
            ?? _leadMasterDataService.ResolveIndustry(lead.Category)?.Code;

        if (!string.IsNullOrWhiteSpace(industryContext?.Audience.PrimaryPersona))
        {
            return CreateField(
                key: "target_audience",
                label: "Target audience",
                value: industryContext.Audience.PrimaryPersona,
                confidence: "inferred",
                source: "industry_context",
                reason: $"Derived from {industryContext.Label} industry audience profile ({industryContext.Audience.BuyingJourney}).",
                required: false);
        }

        if (industryCode == LeadCanonicalValues.IndustryCodes.FuneralServices)
        {
            return CreateField(
                key: "target_audience",
                label: "Target audience",
                value: "Family decision-makers (35-65), trust-sensitive",
                confidence: "inferred",
                source: "industry_policy",
                reason: "Derived from funeral-services audience archetype.",
                required: false);
        }

        if (industryCode == LeadCanonicalValues.IndustryCodes.Retail)
        {
            return CreateField(
                key: "target_audience",
                label: "Target audience",
                value: "Price-sensitive local households",
                confidence: "inferred",
                source: "industry_policy",
                reason: "Derived from retail audience archetype.",
                required: false);
        }

        return CreateUnknownField("target_audience", "Target audience", false);
    }

    private static LeadEnrichmentField BuildGenderField(Lead lead, IReadOnlyList<LeadSignalEvidence> evidences)
    {
        var genderEvidence = FindEvidence(evidences, "website_gender_hint");
        if (genderEvidence is not null)
        {
            return CreateField(
                key: "gender",
                label: "Gender",
                value: NormalizeGenderHint(genderEvidence.Value),
                confidence: "inferred",
                source: genderEvidence.Source,
                reason: "Gender focus inferred from website language.",
                required: false);
        }

        if (ContainsAny(lead.Category, "beauty", "salon", "spa"))
        {
            return CreateField(
                key: "gender",
                label: "Gender",
                value: "Female-leaning",
                confidence: "inferred",
                source: "industry_policy",
                reason: "Category archetype indicates female-leaning audience.",
                required: false);
        }

        return CreateUnknownField("gender", "Gender", false);
    }

    private static LeadEnrichmentField BuildBudgetTierField(
        Lead lead,
        IReadOnlyList<LeadChannelDetectionResult> channelDetections,
        Signal? latestSignal)
    {
        var digitalStrength = channelDetections
            .Where(item => item.Channel is "social" or "search")
            .Select(item => item.Score)
            .DefaultIfEmpty(0)
            .Max();

        if (digitalStrength >= 60 || latestSignal?.HasPromo == true)
        {
            return CreateField(
                key: "budget_tier",
                label: "Budget tier",
                value: "Growth SME",
                confidence: "inferred",
                source: "signal_profile",
                reason: "Active campaign signals suggest moderate growth budget capacity.",
                required: false);
        }

        if (!string.IsNullOrWhiteSpace(lead.Website))
        {
            return CreateField(
                key: "budget_tier",
                label: "Budget tier",
                value: "Starter SME",
                confidence: "inferred",
                source: "signal_profile",
                reason: "Web presence exists but campaign intensity is unclear.",
                required: false);
        }

        return CreateUnknownField("budget_tier", "Budget tier", false);
    }

    private static LeadSignalEvidence? FindEvidence(IReadOnlyList<LeadSignalEvidence> evidences, params string[] signalTypes)
    {
        if (evidences.Count == 0 || signalTypes.Length == 0)
        {
            return null;
        }

        return evidences
            .Where(item => signalTypes.Contains(item.SignalType, StringComparer.OrdinalIgnoreCase))
            .OrderByDescending(item => item.ObservedAt)
            .ThenByDescending(item => item.CreatedAt)
            .FirstOrDefault();
    }

    private IReadOnlyList<DetectedLanguage> ResolveDetectedLanguages(IReadOnlyList<LeadSignalEvidence> evidences)
    {
        if (evidences.Count == 0)
        {
            return Array.Empty<DetectedLanguage>();
        }

        return evidences
            .Where(item =>
                item.SignalType.Equals("social_language_detected", StringComparison.OrdinalIgnoreCase)
                || item.SignalType.Equals("website_language_detected", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(item => item.ObservedAt)
            .ThenByDescending(item => item.CreatedAt)
            .Select(item =>
            {
                var resolved = _leadMasterDataService.ResolveLanguage(item.Value);
                return new DetectedLanguage(
                    string.IsNullOrWhiteSpace(resolved?.Label) ? item.Value.Trim() : resolved.Label,
                    item.Source);
            })
            .Where(item => !string.IsNullOrWhiteSpace(item.Value))
            .GroupBy(item => item.Value, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .Take(2)
            .ToArray();
    }

    private static LeadEnrichmentField CreateField(
        string key,
        string label,
        string value,
        string confidence,
        string source,
        string reason,
        bool required)
    {
        return new LeadEnrichmentField
        {
            Key = key,
            Label = label,
            Value = value,
            Confidence = confidence,
            Source = source,
            Reason = reason,
            Required = required
        };
    }

    private static LeadEnrichmentField CreateUnknownField(string key, string label, bool required)
    {
        return new LeadEnrichmentField
        {
            Key = key,
            Label = label,
            Value = string.Empty,
            Confidence = "unknown",
            Source = "none",
            Reason = "No reliable evidence found.",
            Required = required
        };
    }

    private static bool ContainsAny(string? value, params string[] tokens)
    {
        if (string.IsNullOrWhiteSpace(value) || tokens.Length == 0)
        {
            return false;
        }

        return tokens.Any(token => value.Contains(token, StringComparison.OrdinalIgnoreCase));
    }

    private static string FormatChannel(string channel)
    {
        return channel switch
        {
            "billboards_ooh" => "Billboards and Digital Screens",
            _ => string.Join(" ", channel.Split('_', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(part => char.ToUpperInvariant(part[0]) + part[1..]))
        };
    }

    private static string NormalizeAudienceHint(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "General audience";
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "families" => "Families and household decision-makers",
            "professionals" => "Working professionals",
            "students" => "Students and early-career adults",
            "parents" => "Parents and family decision-makers",
            "smes" => "Small and medium business owners",
            "entrepreneurs" => "Entrepreneurs and owner-managed businesses",
            "homeowners" => "Homeowners and household buyers",
            "commuters" => "Daily commuters and transit audiences",
            "youth" => "Youth and younger adults",
            _ => ToSentenceCase(value)
        };
    }

    private static string NormalizeGenderHint(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "Broad";
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "women" or "female" or "ladies" => "Female-leaning",
            "men" or "male" or "gents" => "Male-leaning",
            _ => "Broad"
        };
    }

    private static string ToSentenceCase(string value)
    {
        var trimmed = value.Trim();
        return trimmed.Length switch
        {
            0 => string.Empty,
            1 => trimmed.ToUpperInvariant(),
            _ => char.ToUpperInvariant(trimmed[0]) + trimmed[1..].ToLowerInvariant()
        };
    }

    private static decimal CalculateConfidenceScore(IReadOnlyList<LeadEnrichmentField> fields)
    {
        if (fields.Count == 0)
        {
            return 0m;
        }

        decimal WeightFor(LeadEnrichmentField field)
        {
            return field.Confidence.ToLowerInvariant() switch
            {
                "detected" => 1.0m,
                "inferred" => 0.6m,
                _ => 0.0m
            };
        }

        var totalWeight = fields.Sum(WeightFor);
        var score = totalWeight / fields.Count;
        return Math.Round(score, 2, MidpointRounding.AwayFromZero);
    }

    private sealed class NoOpGeocodingService : IGeocodingService
    {
        public GeocodingResolution ResolveLocation(string? rawLocation)
        {
            return new GeocodingResolution
            {
                IsResolved = false,
                CanonicalLocation = rawLocation?.Trim() ?? string.Empty,
                Source = "none"
            };
        }

        public GeocodingResolution ResolveCampaignTarget(Contracts.Campaigns.CampaignPlanningRequest request)
        {
            return new GeocodingResolution
            {
                IsResolved = false,
                CanonicalLocation = string.Empty,
                Source = "none"
            };
        }
    }

    private sealed class NoOpLeadMasterDataService : ILeadMasterDataService
    {
        public LeadMasterTokenSet GetTokenSet() => new();
        public MasterLocationMatch? ResolveLocation(string? value) => null;
        public MasterIndustryMatch? ResolveIndustry(string? value) => null;
        public MasterIndustryMatch? ResolveIndustryFromHints(IReadOnlyList<string> hints) => null;
        public MasterLanguageMatch? ResolveLanguage(string? value) => null;
    }

    private sealed record DetectedLanguage(string Value, string Source);
}
