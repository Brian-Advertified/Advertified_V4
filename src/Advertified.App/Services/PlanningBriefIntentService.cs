using Advertified.App.Contracts.Campaigns;
using Advertified.App.Domain.Campaigns;
using Advertified.App.Services.Abstractions;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Advertified.App.Services;

public sealed class PlanningBriefIntentService : IPlanningBriefIntentService
{
    private const string MatchCountKey = "briefIntentMatchCount";
    private const string ConsideredCountKey = "briefIntentConsideredCount";
    private const string RequiredCountKey = "briefIntentRequiredCount";
    private const string EligibleKey = "briefIntentEligible";
    private const string AudienceEvidenceKey = "briefIntentAudienceEvidence";
    private const string ScoreBonusKey = "briefIntentScoreBonus";
    private const string DistanceKmKey = "briefIntentDistanceKm";
    private const string MatchedDimensionsKey = "briefIntentMatchedDimensions";
    private const string MissingDimensionsKey = "briefIntentMissingDimensions";
    private const string PolicyFlagsKey = "briefIntentPolicyFlags";

    private readonly PlanningBriefIntentSettingsSnapshotProvider _settingsProvider;
    private readonly IBroadcastMasterDataService _broadcastMasterDataService;

    public PlanningBriefIntentService(
        PlanningBriefIntentSettingsSnapshotProvider settingsProvider,
        IBroadcastMasterDataService broadcastMasterDataService)
    {
        _settingsProvider = settingsProvider;
        _broadcastMasterDataService = broadcastMasterDataService;
    }

    public PlanningBriefIntentEvaluation EvaluateCandidate(InventoryCandidate candidate, CampaignPlanningRequest request)
    {
        var settings = _settingsProvider.GetCurrent();
        var matched = new List<string>();
        var missing = new List<string>();
        var flags = new List<string>();

        var audienceEvidencePresent = HasAudienceMetadata(candidate);
        if (candidate.MediaType.Equals("OOH", StringComparison.OrdinalIgnoreCase)
            && NormalizeScope(request.GeographyScope) == "local"
            && settings.RequireLocalOohAudienceEvidence)
        {
            flags.Add("brief_intent_local_ooh_enforced");
            if (!audienceEvidencePresent)
            {
                missing.Add("audience_evidence");
            }
        }

        EvaluateLanguage(candidate, request, matched, missing);
        EvaluateAge(candidate, request, matched, missing);
        EvaluateGender(candidate, request, matched, missing);
        EvaluatePricePositioning(candidate, request, matched, missing);
        EvaluateBuyingBehaviour(candidate, request, matched, missing);
        EvaluateCustomerType(candidate, request, matched, missing);
        EvaluateAudienceTerms(candidate, request, matched, missing);

        var consideredCount = matched.Count + missing.Count;
        var requiredCount = ResolveRequiredCount(candidate, request, settings, consideredCount);
        var distanceKm = ResolveDistanceKm(candidate, request);
        if (distanceKm.HasValue && candidate.MediaType.Equals("OOH", StringComparison.OrdinalIgnoreCase) && NormalizeScope(request.GeographyScope) == "local")
        {
            var allowedRadius = IsRelaxedPass(request)
                ? settings.RelaxedLocalOohRadiusKm
                : settings.LocalOohRadiusKm;

            if (distanceKm.Value > allowedRadius)
            {
                missing.Add("local_radius");
                flags.Add("brief_intent_radius_filtered");
            }
            else
            {
                matched.Add("local_radius");
            }
        }

        var matchedDistinct = matched.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var missingDistinct = missing.Distinct(StringComparer.OrdinalIgnoreCase)
            .Except(matchedDistinct, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var matchedCount = matchedDistinct.Length;
        var allConsidered = matchedCount + missingDistinct.Length;
        requiredCount = Math.Min(requiredCount, allConsidered);

        var isEligible = (!settings.RequireLocalOohAudienceEvidence || audienceEvidencePresent || !IsStrictLocalOoh(candidate, request))
            && matchedCount >= requiredCount;
        if (!isEligible)
        {
            flags.Add("brief_intent_mismatch");
        }

        var scoreBonus = matchedCount <= 0
            ? 0m
            : (matchedCount * settings.ScorePerMatch) + (missingDistinct.Length == 0 ? settings.FullMatchBonus : 0m);

        var evaluation = new PlanningBriefIntentEvaluation
        {
            ConsideredDimensionCount = allConsidered,
            MatchedDimensionCount = matchedCount,
            RequiredDimensionCount = requiredCount,
            IsEligible = isEligible,
            AudienceEvidencePresent = audienceEvidencePresent,
            ScoreBonus = scoreBonus,
            DistanceKm = distanceKm,
            MatchedDimensions = matchedDistinct,
            MissingDimensions = missingDistinct,
            PolicyFlags = flags.Distinct(StringComparer.OrdinalIgnoreCase).ToArray()
        };

        ApplyMetadata(candidate, evaluation);
        return evaluation;
    }

    private void EvaluateLanguage(InventoryCandidate candidate, CampaignPlanningRequest request, ICollection<string> matched, ICollection<string> missing)
    {
        if (request.TargetLanguages.Count == 0)
        {
            return;
        }

        var requested = request.TargetLanguages
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(_broadcastMasterDataService.NormalizeLanguageForMatching)
            .Where(static value => value.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (requested.Length == 0)
        {
            return;
        }

        var candidateLanguages = ExtractMetadataTokens(candidate.Language)
            .Concat(ExtractMetadataTokens(GetMetadataValue(candidate, "primaryLanguages", "primary_languages", "language", "secondaryLanguage", "secondary_language")))
            .Select(_broadcastMasterDataService.NormalizeLanguageForMatching)
            .Where(static value => value.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (requested.Any(language => candidateLanguages.Contains(language, StringComparer.OrdinalIgnoreCase)))
        {
            matched.Add("languages");
        }
        else
        {
            missing.Add("languages");
        }
    }

    private static void EvaluateAge(InventoryCandidate candidate, CampaignPlanningRequest request, ICollection<string> matched, ICollection<string> missing)
    {
        if (!request.TargetAgeMin.HasValue && !request.TargetAgeMax.HasValue)
        {
            return;
        }

        var requestedMin = request.TargetAgeMin ?? request.TargetAgeMax ?? 13;
        var requestedMax = request.TargetAgeMax ?? request.TargetAgeMin ?? 100;
        var ageText = GetMetadataText(candidate, "audienceAgeSkew", "audience_age_skew", "targetAudience", "target_audience");
        if (string.IsNullOrWhiteSpace(ageText))
        {
            missing.Add("age");
            return;
        }

        if (TryParseAgeRange(ageText, out var candidateMin, out var candidateMax)
            && !(candidateMax < requestedMin || candidateMin > requestedMax))
        {
            matched.Add("age");
            return;
        }

        missing.Add("age");
    }

    private static void EvaluateGender(InventoryCandidate candidate, CampaignPlanningRequest request, ICollection<string> matched, ICollection<string> missing)
    {
        var targetGender = NormalizeGender(request.TargetGender);
        if (string.IsNullOrWhiteSpace(targetGender) || targetGender == "all")
        {
            return;
        }

        var genderText = GetMetadataText(candidate, "audienceGenderSkew", "audience_gender_skew", "targetAudience", "target_audience");
        if (string.IsNullOrWhiteSpace(genderText))
        {
            missing.Add("gender");
            return;
        }

        if (GenderAliases(targetGender).Any(alias => genderText.Contains(alias, StringComparison.OrdinalIgnoreCase)))
        {
            matched.Add("gender");
            return;
        }

        missing.Add("gender");
    }

    private static void EvaluatePricePositioning(InventoryCandidate candidate, CampaignPlanningRequest request, ICollection<string> matched, ICollection<string> missing)
    {
        if (string.IsNullOrWhiteSpace(request.PricePositioning))
        {
            return;
        }

        if (MatchesMetadataToken(candidate, request.PricePositioning, "pricePositioningFit", "price_positioning_fit", "premiumMassFit", "premium_mass_fit"))
        {
            matched.Add("price_positioning");
        }
        else
        {
            missing.Add("price_positioning");
        }
    }

    private static void EvaluateBuyingBehaviour(InventoryCandidate candidate, CampaignPlanningRequest request, ICollection<string> matched, ICollection<string> missing)
    {
        if (string.IsNullOrWhiteSpace(request.BuyingBehaviour))
        {
            return;
        }

        if (MatchesMetadataToken(candidate, request.BuyingBehaviour, "buyingBehaviourFit", "buying_behaviour_fit"))
        {
            matched.Add("buying_behaviour");
        }
        else
        {
            missing.Add("buying_behaviour");
        }
    }

    private static void EvaluateCustomerType(InventoryCandidate candidate, CampaignPlanningRequest request, ICollection<string> matched, ICollection<string> missing)
    {
        if (string.IsNullOrWhiteSpace(request.CustomerType))
        {
            return;
        }

        var audienceText = BuildAudienceSearchText(candidate);
        var tokens = request.CustomerType.Trim().ToLowerInvariant() switch
        {
            "retail" => new[] { "retail", "shopper", "mall", "fashion", "grocery", "lifestyle" },
            "corporate" => new[] { "professional", "business", "executive", "office", "corporate" },
            "government" => new[] { "government", "public", "civic", "commuter" },
            _ => TokenizeTerms(request.CustomerType).ToArray()
        };

        if (tokens.Any(token => audienceText.Contains(token, StringComparison.OrdinalIgnoreCase)))
        {
            matched.Add("customer_type");
        }
        else
        {
            missing.Add("customer_type");
        }
    }

    private static void EvaluateAudienceTerms(InventoryCandidate candidate, CampaignPlanningRequest request, ICollection<string> matched, ICollection<string> missing)
    {
        var requestedTokens = request.TargetInterests
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .SelectMany(TokenizeTerms)
            .Concat(TokenizeTerms(request.TargetAudienceNotes))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(12)
            .ToArray();
        if (requestedTokens.Length == 0)
        {
            return;
        }

        var audienceText = BuildAudienceSearchText(candidate);
        if (requestedTokens.Any(token => audienceText.Contains(token, StringComparison.OrdinalIgnoreCase)))
        {
            matched.Add("audience_terms");
        }
        else
        {
            missing.Add("audience_terms");
        }
    }

    private static int ResolveRequiredCount(
        InventoryCandidate candidate,
        CampaignPlanningRequest request,
        PlanningBriefIntentSettingsSnapshot settings,
        int consideredCount)
    {
        if (!IsStrictLocalOoh(candidate, request) || consideredCount <= 0)
        {
            return 0;
        }

        var baseRequirement = settings.LocalOohMinDimensionMatches;
        if (IsRelaxedPass(request))
        {
            baseRequirement = Math.Max(1, baseRequirement - 1);
        }

        return Math.Min(baseRequirement, consideredCount);
    }

    private static bool IsStrictLocalOoh(InventoryCandidate candidate, CampaignPlanningRequest request)
    {
        return candidate.MediaType.Equals("OOH", StringComparison.OrdinalIgnoreCase)
            && NormalizeScope(request.GeographyScope) == "local";
    }

    private static bool IsRelaxedPass(CampaignPlanningRequest request)
    {
        return string.Equals(request.TargetLocationSource, "geography_relaxed", StringComparison.OrdinalIgnoreCase);
    }

    private static double? ResolveDistanceKm(InventoryCandidate candidate, CampaignPlanningRequest request)
    {
        if (!candidate.Latitude.HasValue || !candidate.Longitude.HasValue || !request.TargetLatitude.HasValue || !request.TargetLongitude.HasValue)
        {
            return null;
        }

        return HaversineDistanceKm(
            request.TargetLatitude.Value,
            request.TargetLongitude.Value,
            candidate.Latitude.Value,
            candidate.Longitude.Value);
    }

    private static void ApplyMetadata(InventoryCandidate candidate, PlanningBriefIntentEvaluation evaluation)
    {
        candidate.Metadata[MatchCountKey] = evaluation.MatchedDimensionCount;
        candidate.Metadata[ConsideredCountKey] = evaluation.ConsideredDimensionCount;
        candidate.Metadata[RequiredCountKey] = evaluation.RequiredDimensionCount;
        candidate.Metadata[EligibleKey] = evaluation.IsEligible;
        candidate.Metadata[AudienceEvidenceKey] = evaluation.AudienceEvidencePresent;
        candidate.Metadata[ScoreBonusKey] = evaluation.ScoreBonus;
        candidate.Metadata[MatchedDimensionsKey] = evaluation.MatchedDimensions.ToArray();
        candidate.Metadata[MissingDimensionsKey] = evaluation.MissingDimensions.ToArray();
        candidate.Metadata[PolicyFlagsKey] = evaluation.PolicyFlags.ToArray();

        if (evaluation.DistanceKm.HasValue)
        {
            candidate.Metadata[DistanceKmKey] = decimal.Round((decimal)evaluation.DistanceKm.Value, 2);
        }
    }

    private static bool HasAudienceMetadata(InventoryCandidate candidate)
    {
        return !string.IsNullOrWhiteSpace(GetMetadataText(
            candidate,
            "audienceKeywords",
            "audience_keywords",
            "targetAudience",
            "target_audience",
            "audienceAgeSkew",
            "audience_age_skew",
            "audienceGenderSkew",
            "audience_gender_skew",
            "buyingBehaviourFit",
            "buying_behaviour_fit",
            "pricePositioningFit",
            "price_positioning_fit",
            "venueType",
            "venue_type",
            "premiumMassFit",
            "premium_mass_fit",
            "highValueShopperFit",
            "high_value_shopper_fit",
            "youthFit",
            "youth_fit",
            "familyFit",
            "family_fit",
            "professionalFit",
            "professional_fit",
            "commuterFit",
            "commuter_fit"));
    }

    private static string BuildAudienceSearchText(InventoryCandidate candidate)
    {
        var parts = new[]
            {
                candidate.DisplayName,
                candidate.MediaType,
                candidate.Subtype,
                candidate.Language,
                candidate.Area,
                candidate.Suburb,
                candidate.City,
                GetMetadataText(candidate, "targetAudience", "target_audience", "notes", "venueType", "venue_type", "environmentType", "environment_type", "premiumMassFit", "premium_mass_fit", "pricePositioningFit", "price_positioning_fit", "highValueShopperFit", "high_value_shopper_fit", "buyingBehaviourFit", "buying_behaviour_fit", "professionalFit", "professional_fit")
            }
            .Where(static part => !string.IsNullOrWhiteSpace(part))
            .ToList();

        foreach (var key in new[] { "audienceKeywords", "audience_keywords", "primaryAudienceTags", "primary_audience_tags", "secondaryAudienceTags", "secondary_audience_tags", "recommendationTags", "recommendation_tags" })
        {
            parts.AddRange(ExtractMetadataTokens(GetMetadataValue(candidate, key)));
        }

        return string.Join(" ", parts);
    }

    private static bool MatchesMetadataToken(InventoryCandidate candidate, string requestedValue, params string[] keys)
    {
        return keys.Any(key =>
            candidate.Metadata.TryGetValue(key, out var value)
            && ExtractMetadataTokens(value).Any(token => MatchesStrategyToken(requestedValue, token)));
    }

    private static object? GetMetadataValue(InventoryCandidate candidate, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (candidate.Metadata.TryGetValue(key, out var value))
            {
                return value;
            }
        }

        return null;
    }

    private static string GetMetadataText(InventoryCandidate candidate, params string[] keys)
    {
        return string.Join(" ", ExtractMetadataTokens(GetMetadataValue(candidate, keys)));
    }

    private static IEnumerable<string> ExtractMetadataTokens(object? value)
    {
        if (value is null)
        {
            yield break;
        }

        if (value is string text)
        {
            if (!string.IsNullOrWhiteSpace(text))
            {
                yield return text.Trim();
            }

            yield break;
        }

        if (value is IEnumerable<string> textValues)
        {
            foreach (var entry in textValues)
            {
                if (!string.IsNullOrWhiteSpace(entry))
                {
                    yield return entry.Trim();
                }
            }

            yield break;
        }

        if (value is JsonElement json)
        {
            if (json.ValueKind == JsonValueKind.String)
            {
                var jsonText = json.GetString();
                if (!string.IsNullOrWhiteSpace(jsonText))
                {
                    yield return jsonText.Trim();
                }

                yield break;
            }

            if (json.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in json.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String)
                    {
                        var itemText = item.GetString();
                        if (!string.IsNullOrWhiteSpace(itemText))
                        {
                            yield return itemText.Trim();
                        }
                    }
                }
            }

            yield break;
        }

        var fallback = value.ToString();
        if (!string.IsNullOrWhiteSpace(fallback))
        {
            yield return fallback.Trim();
        }
    }

    private static IEnumerable<string> TokenizeTerms(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            yield break;
        }

        foreach (Match match in Regex.Matches(text, "[A-Za-z]{4,}"))
        {
            yield return match.Value.Trim().ToLowerInvariant();
        }
    }

    private static bool MatchesStrategyToken(string requestedValue, string metadataToken)
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

    private static string NormalizeStrategyToken(string value)
    {
        return value
            .Trim()
            .ToLowerInvariant()
            .Replace('|', ' ')
            .Replace('/', ' ')
            .Replace('-', '_')
            .Replace(' ', '_');
    }

    private static bool TryParseAgeRange(string text, out int min, out int max)
    {
        min = 0;
        max = 0;

        var normalized = text.Trim().ToLowerInvariant();
        var values = Regex.Matches(normalized, "\\d+")
            .Select(match => int.TryParse(match.Value, out var parsed) ? parsed : (int?)null)
            .Where(value => value.HasValue)
            .Select(value => value!.Value)
            .ToArray();

        if (normalized.Contains('+') && values.Length >= 1)
        {
            min = values[0];
            max = 100;
            return true;
        }

        if (values.Length >= 2)
        {
            min = Math.Min(values[0], values[1]);
            max = Math.Max(values[0], values[1]);
            return true;
        }

        return false;
    }

    private static string NormalizeGender(string? value)
    {
        var normalized = (value ?? string.Empty).Trim().ToLowerInvariant();
        return normalized switch
        {
            "male" or "man" or "men" => "male",
            "female" or "woman" or "women" => "female",
            "all" or "everyone" or "mixed" => "all",
            _ => string.Empty
        };
    }

    private static IEnumerable<string> GenderAliases(string normalizedGender)
    {
        return normalizedGender switch
        {
            "male" => new[] { "male", "men", "man", "guy", "gent" },
            "female" => new[] { "female", "women", "woman", "lady" },
            _ => Array.Empty<string>()
        };
    }

    private static string NormalizeScope(string? scope)
    {
        var normalized = (scope ?? string.Empty).Trim().ToLowerInvariant();
        return normalized switch
        {
            "regional" => "provincial",
            "local" => "local",
            "provincial" => "provincial",
            "national" => "national",
            _ => normalized
        };
    }

    private static double HaversineDistanceKm(double lat1, double lon1, double lat2, double lon2)
    {
        const double radiusKm = 6371.0;
        static double ToRadians(double angle) => Math.PI * angle / 180.0;

        var dLat = ToRadians(lat2 - lat1);
        var dLon = ToRadians(lon2 - lon1);
        var a = Math.Pow(Math.Sin(dLat / 2), 2)
                + Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) * Math.Pow(Math.Sin(dLon / 2), 2);
        var c = 2 * Math.Asin(Math.Min(1, Math.Sqrt(a)));
        return radiusKm * c;
    }
}
