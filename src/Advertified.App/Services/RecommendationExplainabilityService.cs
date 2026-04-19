using Advertified.App.Contracts.Campaigns;
using Advertified.App.Domain.Campaigns;
using Advertified.App.Services.Abstractions;

namespace Advertified.App.Services;

public sealed class RecommendationExplainabilityService : IRecommendationExplainabilityService
{
    private readonly IPlanningScoreService _scoreService;
    private readonly IPlanningPolicyService _policyService;

    public RecommendationExplainabilityService(IPlanningScoreService scoreService, IPlanningPolicyService policyService)
    {
        _scoreService = scoreService;
        _policyService = policyService;
    }

    public PlanningCandidateAnalysis AnalyzeCandidate(InventoryCandidate candidate, CampaignPlanningRequest request)
    {
        var score = _scoreService.AnalyzeCandidate(candidate, request).Score;
        var selectionReasons = BuildSelectionReasons(candidate, request);
        var policyFlags = BuildPolicyFlags(candidate, request);
        var confidenceScore = CalculateConfidenceScore(candidate, request);
        return new PlanningCandidateAnalysis(score, selectionReasons, policyFlags, confidenceScore);
    }

    public string BuildRationale(List<PlannedItem> basePlan, List<PlannedItem> recommendedPlan, CampaignPlanningRequest request)
    {
        var mediaMix = string.Join(", ", recommendedPlan.Select(x => ToDisplayMediaType(x.MediaType)).Distinct());
        var targetMix = _policyService.BuildRequestedMixLabel(request);
        var strategySignals = CampaignStrategySupport.BuildSignals(request);
        var strategySummary = BuildStrategySummary(strategySignals);
        var locationSummary = BuildLocationSummary(request);
        return string.IsNullOrWhiteSpace(targetMix)
            ? $"Plan built within budget of {request.SelectedBudget:n0}, prioritising geography fit, audience fit, business context, media preference, and available inventory. Selected mix: {mediaMix}.{locationSummary}{strategySummary}"
            : $"Plan built within budget of {request.SelectedBudget:n0}, prioritising geography fit, audience fit, business context, media preference, requested mix targets, and available inventory. Selected mix: {mediaMix}. Requested target: {targetMix}.{locationSummary}{strategySummary}";
    }

    public IReadOnlyList<string> GetPreferredMediaFallbackFlags(
        CampaignPlanningRequest request,
        List<PlannedItem> recommendedPlan,
        IReadOnlyList<InventoryCandidate> eligibleCandidates)
    {
        if (request.PreferredMediaTypes.Count == 0 || recommendedPlan.Count == 0)
        {
            return Array.Empty<string>();
        }

        var selectedMedia = recommendedPlan
            .Select(item => item.MediaType.Trim().ToLowerInvariant())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return request.PreferredMediaTypes
            .Select(preferred => preferred.Trim().ToLowerInvariant())
            .Where(preferred => !string.IsNullOrWhiteSpace(preferred))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(preferred => _policyService.GetTargetShare(preferred, request).GetValueOrDefault() > 0)
            .Where(preferred => !selectedMedia.Contains(preferred))
            .Select(preferred => $"preferred_media_unfulfilled:{preferred}")
            .ToArray();
    }

    private string[] BuildSelectionReasons(InventoryCandidate candidate, CampaignPlanningRequest request)
    {
        var reasons = new List<string>();

        if (_scoreService.GeoScore(candidate, request) >= 22m) reasons.Add("Strong geography match");
        else if (_scoreService.GeoScore(candidate, request) >= 16m) reasons.Add("Good regional alignment");

        if (MatchesBusinessOrigin(candidate, request))
        {
            reasons.Add("Close to the business origin");
        }

        if (MatchesPriorityArea(candidate, request))
        {
            reasons.Add("Supports a high-priority target area");
        }

        if (_scoreService.AudienceScore(candidate, request) >= 15m) reasons.Add("Audience profile overlap");
        else if (_scoreService.AudienceScore(candidate, request) >= 10m) reasons.Add("Language or audience fit");

        if (_scoreService.MediaPreferenceScore(candidate, request) >= 15m) reasons.Add("Matches requested channel mix");
        if (_scoreService.MixTargetScore(candidate, request) >= 8m) reasons.Add("Supports requested mix target");
        if (_scoreService.BudgetScore(candidate, request) >= 12m) reasons.Add("Fits comfortably within budget");
        if (_scoreService.IndustryContextFitScore(candidate, request) >= 8m) reasons.Add("Strong industry and context fit");
        if (SupportsObjective(candidate, request)) reasons.Add("Supports campaign objective");
        if (_scoreService.AnalyzeCandidate(candidate, request).Score >= 75m) reasons.Add("Strong overall strategic fit");
        reasons.AddRange(GetBriefIntentReasons(candidate));

        var strategySignals = CampaignStrategySupport.BuildSignals(request);
        var mediaType = candidate.MediaType.Trim().ToLowerInvariant();
        if (strategySignals.PremiumAudience && mediaType is "ooh" or "tv")
        {
            reasons.Add("Supports premium positioning");
        }

        if (strategySignals.FastDecisionCycle && mediaType is "radio" or "digital")
        {
            reasons.Add("Useful for faster buying cycles");
        }

        if (strategySignals.WalkInDriven && mediaType == "ooh")
        {
            reasons.Add("Supports walk-in footfall");
        }

        if (strategySignals.AudienceNeedsBroadReach && mediaType is "ooh" or "tv")
        {
            reasons.Add("Useful for broad audience discovery");
        }

        if (candidate.MediaType.Equals("Radio", StringComparison.OrdinalIgnoreCase))
        {
            if (SupportsAllRequestedLanguages(candidate, request))
            {
                reasons.Add("Covers the full requested language mix");
            }
            else if (SupportsTopRequestedLanguage(candidate, request))
            {
                reasons.Add("Supports the highest-priority requested language");
            }

            if (IsPackageTotalCandidate(candidate)) reasons.Add("Fixed supplier package investment");
            else if (IsPerSpotRateCardCandidate(candidate)) reasons.Add("Per-spot rate card pricing");

            if (!string.IsNullOrWhiteSpace(candidate.TimeBand)
                && (Matches(candidate.TimeBand, "breakfast") || Matches(candidate.TimeBand, "drive")))
            {
                reasons.Add("High-impact radio daypart");
            }

            if (_policyService.IsNationalCapableRadioCandidate(candidate, request))
            {
                reasons.Add("Supports higher-band radio policy");
            }
        }

        if (candidate.MediaType.Equals("OOH", StringComparison.OrdinalIgnoreCase))
        {
            reasons.Add("Billboards and Digital Screens prioritized for visibility");
            reasons.Add("Adds visible market presence");

            if (MatchesMetadataToken(candidate, "premium", "premiumMassFit", "premium_mass_fit"))
            {
                reasons.Add("Premium venue audience fit");
            }

            if (MatchesMetadataToken(candidate, "premium_mall", "venueType", "venue_type"))
            {
                reasons.Add("Placed in a premium mall environment");
            }

            if (MatchesMetadataToken(candidate, "mall_interior", "environmentType", "environment_type")
                || MatchesMetadataToken(candidate, "food_court", "environmentType", "environment_type"))
            {
                reasons.Add("Benefits from strong dwell-time environment");
            }

            if (MatchesMetadataToken(candidate, "high", "youthFit", "youth_fit"))
            {
                reasons.Add("Strong youth audience signal");
            }

            if (MatchesMetadataToken(candidate, "high", "familyFit", "family_fit"))
            {
                reasons.Add("Strong family shopper signal");
            }

            if (MatchesMetadataToken(candidate, "high", "professionalFit", "professional_fit"))
            {
                reasons.Add("Strong professional audience signal");
            }
        }

        if (candidate.MediaType.Equals("TV", StringComparison.OrdinalIgnoreCase))
        {
            if (SupportsAllRequestedLanguages(candidate, request))
            {
                reasons.Add("Covers the full requested language mix");
            }
            else if (SupportsTopRequestedLanguage(candidate, request))
            {
                reasons.Add("Supports the highest-priority requested language");
            }
        }

        return reasons.Distinct(StringComparer.OrdinalIgnoreCase).Take(4).ToArray();
    }

    private string[] BuildPolicyFlags(InventoryCandidate candidate, CampaignPlanningRequest request)
    {
        var flags = new List<string>();

        if (request.SelectedBudget >= 1000000m) flags.Add("dominance_policy");
        else if (request.SelectedBudget >= 500000m) flags.Add("scale_policy");

        if (request.PreferredMediaTypes.Count > 0) flags.Add("preferred_media_applied");
        if (candidate.MediaType.Equals("Radio", StringComparison.OrdinalIgnoreCase) && _policyService.IsNationalCapableRadioCandidate(candidate, request))
        {
            flags.Add("national_capable_radio");
        }

        flags.Add(_policyService.GetPricingModel(candidate));

        if (candidate.MediaType.Equals("OOH", StringComparison.OrdinalIgnoreCase)) flags.Add("ooh_priority");
        if (candidate.Metadata.TryGetValue("briefIntentPolicyFlags", out var briefIntentFlags)
            && briefIntentFlags is IEnumerable<string> values)
        {
            flags.AddRange(values);
        }

        var targetShare = _policyService.GetTargetShare(candidate.MediaType, request);
        if (targetShare.HasValue) flags.Add($"mix_target_{candidate.MediaType.Trim().ToLowerInvariant()}_{targetShare.Value}");
        if (!string.IsNullOrWhiteSpace(candidate.RegionClusterCode)) flags.Add($"region:{candidate.RegionClusterCode.Trim().ToLowerInvariant()}");

        return flags.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static decimal CalculateConfidenceScore(InventoryCandidate candidate, CampaignPlanningRequest request)
    {
        var score = 0.45m;
        if (!string.IsNullOrWhiteSpace(candidate.RegionClusterCode)) score += 0.05m;
        if (!string.IsNullOrWhiteSpace(candidate.Language)) score += 0.05m;
        if (candidate.MediaType.Equals("Radio", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(candidate.TimeBand)) score += 0.05m;
        if (candidate.MediaType.Equals("Radio", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(candidate.SlotType)) score += 0.03m;
        if (CampaignStrategySupport.BuildSignals(request).AudienceClearlyDefined) score += 0.04m;
        score += ResolveDataConfidenceBonus(candidate);
        return Math.Min(0.95m, decimal.Round(score, 2));
    }

    private static decimal ResolveDataConfidenceBonus(InventoryCandidate candidate)
    {
        if (!candidate.Metadata.TryGetValue("data_confidence", out var value)
            && !candidate.Metadata.TryGetValue("dataConfidence", out value))
        {
            return 0m;
        }

        var raw = value?.ToString()?.Trim().ToLowerInvariant();
        return raw switch
        {
            "high" => 0.12m,
            "medium" => 0.06m,
            "low" => 0.02m,
            _ => 0m
        };
    }

    private static string BuildStrategySummary(CampaignStrategySignals signals)
    {
        if (signals.PremiumAudience)
        {
            return " Strategy weighting favoured premium audience alignment.";
        }

        if (signals.FastDecisionCycle || signals.ImmediateUrgency)
        {
            return " Strategy weighting favoured faster-response channels.";
        }

        if (signals.AudienceNeedsBroadReach)
        {
            return " Strategy weighting favoured broader-reach inventory because the audience is less defined.";
        }

        return string.Empty;
    }

    private static string BuildLocationSummary(CampaignPlanningRequest request)
    {
        var coverage = request.Targeting?.Label
            ?? request.TargetLocationLabel
            ?? request.TargetLocationCity
            ?? request.TargetLocationProvince;
        var origin = request.BusinessLocation?.Area
            ?? request.BusinessLocation?.City
            ?? request.BusinessLocation?.Province;

        if (!string.IsNullOrWhiteSpace(origin) && !string.IsNullOrWhiteSpace(coverage))
        {
            return $" Coverage focused on {coverage} while retaining origin bias around {origin}.{BuildAllocationSummary(request)}";
        }

        if (!string.IsNullOrWhiteSpace(origin))
        {
            return $" Origin bias was retained around {origin}.{BuildAllocationSummary(request)}";
        }

        return BuildAllocationSummary(request);
    }

    private static string BuildAllocationSummary(CampaignPlanningRequest request)
    {
        var allocation = request.BudgetAllocation;
        if (allocation is null || allocation.ChannelAllocations.Count == 0 || allocation.GeoAllocations.Count == 0)
        {
            return string.Empty;
        }

        var topChannel = allocation.ChannelAllocations.OrderByDescending(entry => entry.Weight).First();
        var topGeo = allocation.GeoAllocations.OrderByDescending(entry => entry.Weight).First();
        return $" Budget allocation favored {ToDisplayMediaType(topChannel.Channel)} ({topChannel.Weight:P0}) and the {topGeo.Bucket} geo bucket ({topGeo.Weight:P0}).";
    }

    private bool IsPackageTotalCandidate(InventoryCandidate candidate) => _policyService.GetPricingModel(candidate).Equals("package_total", StringComparison.OrdinalIgnoreCase) || candidate.PackageOnly;

    private bool IsPerSpotRateCardCandidate(InventoryCandidate candidate) => _policyService.GetPricingModel(candidate).Equals("per_spot_rate_card", StringComparison.OrdinalIgnoreCase);

    private static bool SupportsAllRequestedLanguages(InventoryCandidate candidate, CampaignPlanningRequest request)
    {
        if (request.TargetLanguages.Count < 2)
        {
            return false;
        }

        return request.TargetLanguages.All(language =>
            MatchesMetadataToken(candidate, language, "primaryLanguages", "primary_languages", "language", "secondaryLanguage", "secondary_language"));
    }

    private static bool SupportsTopRequestedLanguage(InventoryCandidate candidate, CampaignPlanningRequest request)
    {
        var topLanguage = request.TargetLanguages.FirstOrDefault();
        return !string.IsNullOrWhiteSpace(topLanguage)
            && MatchesMetadataToken(candidate, topLanguage, "primaryLanguages", "primary_languages", "language", "secondaryLanguage", "secondary_language");
    }

    private static bool MatchesMetadataToken(InventoryCandidate candidate, string requestedValue, params string[] keys)
    {
        return keys.Any(key =>
            candidate.Metadata.TryGetValue(key, out var value)
            && ExtractMetadataTokens(value).Any(token => MatchesStrategyToken(requestedValue, token)));
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

        if (value is System.Text.Json.JsonElement json)
        {
            if (json.ValueKind == System.Text.Json.JsonValueKind.String)
            {
                var jsonText = json.GetString();
                if (!string.IsNullOrWhiteSpace(jsonText))
                {
                    yield return jsonText.Trim();
                }

                yield break;
            }

            if (json.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                foreach (var item in json.EnumerateArray())
                {
                    if (item.ValueKind == System.Text.Json.JsonValueKind.String)
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

    private static bool Matches(string? left, string? right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right)) return false;
        return string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static string ToDisplayMediaType(string? mediaType)
    {
        if (string.IsNullOrWhiteSpace(mediaType))
        {
            return string.Empty;
        }

        return mediaType.Equals("OOH", StringComparison.OrdinalIgnoreCase)
            ? "Billboards and Digital Screens"
            : mediaType;
    }

    private static bool SupportsObjective(InventoryCandidate candidate, CampaignPlanningRequest request)
    {
        var objective = request.Objective?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(objective))
        {
            return false;
        }

        var mediaType = candidate.MediaType.Trim().ToLowerInvariant();
        return objective switch
        {
            "awareness" or "brand_presence" => mediaType is "ooh" or "tv" or "radio",
            "launch" => mediaType is "ooh" or "tv" or "radio",
            "promotion" => mediaType is "radio" or "ooh" or "digital",
            "leads" => mediaType is "digital" or "radio",
            "foot_traffic" => mediaType is "ooh" or "radio",
            _ => false
        };
    }

    private static IEnumerable<string> GetBriefIntentReasons(InventoryCandidate candidate)
    {
        if (!candidate.Metadata.TryGetValue("briefIntentMatchedDimensions", out var value))
        {
            return Array.Empty<string>();
        }

        var reasons = ExtractMetadataTokens(value)
            .Select(MapBriefIntentReason)
            .Where(static text => !string.IsNullOrWhiteSpace(text))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(2)
            .ToArray();
        return reasons.OfType<string>();
    }

    private static string? MapBriefIntentReason(string dimension)
    {
        return dimension.Trim().ToLowerInvariant() switch
        {
            "languages" => "Supports the requested language mix",
            "age" => "Fits the requested age range",
            "gender" => "Fits the requested gender skew",
            "price_positioning" => "Aligned with the requested price positioning",
            "buying_behaviour" => "Aligned with the requested buying behaviour",
            "customer_type" => "Aligned with the requested customer type",
            "audience_terms" => "Aligned with the requested audience signals",
            "local_radius" => "Stays close to the selected main area",
            _ => null
        };
    }

    private static bool MatchesBusinessOrigin(InventoryCandidate candidate, CampaignPlanningRequest request)
    {
        var businessLocation = request.BusinessLocation;
        if (businessLocation is null)
        {
            return false;
        }

        return Matches(businessLocation.Area, candidate.Suburb)
            || Matches(businessLocation.Area, candidate.Area)
            || Matches(businessLocation.City, candidate.City)
            || Matches(businessLocation.Province, candidate.Province);
    }

    private static bool MatchesPriorityArea(InventoryCandidate candidate, CampaignPlanningRequest request)
    {
        var priorityAreas = request.Targeting?.PriorityAreas ?? request.MustHaveAreas;
        return priorityAreas.Any(area =>
            Matches(area, candidate.Suburb)
            || Matches(area, candidate.Area)
            || Matches(area, candidate.City)
            || Matches(area, candidate.Province));
    }
}

