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
        return string.IsNullOrWhiteSpace(targetMix)
            ? $"Plan built within budget of {request.SelectedBudget:n0}, prioritising geography fit, audience fit, media preference, and available inventory. Selected mix: {mediaMix}."
            : $"Plan built within budget of {request.SelectedBudget:n0}, prioritising geography fit, audience fit, media preference, requested mix targets, and available inventory. Selected mix: {mediaMix}. Requested target: {targetMix}.";
    }

    public IReadOnlyList<string> GetPreferredMediaFallbackFlags(CampaignPlanningRequest request, List<PlannedItem> recommendedPlan)
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
            .Where(preferred => !selectedMedia.Contains(preferred))
            .Select(preferred => $"preferred_media_unfulfilled:{preferred}")
            .ToArray();
    }

    private string[] BuildSelectionReasons(InventoryCandidate candidate, CampaignPlanningRequest request)
    {
        var reasons = new List<string>();

        if (_scoreService.GeoScore(candidate, request) >= 22m) reasons.Add("Strong geography match");
        else if (_scoreService.GeoScore(candidate, request) >= 16m) reasons.Add("Good regional alignment");

        if (_scoreService.AudienceScore(candidate, request) >= 15m) reasons.Add("Audience profile overlap");
        else if (_scoreService.AudienceScore(candidate, request) >= 10m) reasons.Add("Language or audience fit");

        if (_scoreService.MediaPreferenceScore(candidate, request) >= 15m) reasons.Add("Matches requested channel mix");
        if (_scoreService.MixTargetScore(candidate, request) >= 8m) reasons.Add("Supports requested mix target");
        if (_scoreService.BudgetScore(candidate, request) >= 12m) reasons.Add("Fits comfortably within budget");

        if (candidate.MediaType.Equals("Radio", StringComparison.OrdinalIgnoreCase))
        {
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
            reasons.Add("Billboards and digital screens prioritized for visibility");
            reasons.Add("Adds visible market presence");
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
        return Math.Min(0.95m, decimal.Round(score, 2));
    }

    private bool IsPackageTotalCandidate(InventoryCandidate candidate) => _policyService.GetPricingModel(candidate).Equals("package_total", StringComparison.OrdinalIgnoreCase) || candidate.PackageOnly;

    private bool IsPerSpotRateCardCandidate(InventoryCandidate candidate) => _policyService.GetPricingModel(candidate).Equals("per_spot_rate_card", StringComparison.OrdinalIgnoreCase);

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
            ? "Billboards and digital screens"
            : mediaType;
    }
}
