using Advertified.App.Contracts.Campaigns;
using Advertified.App.Domain.Campaigns;
using Advertified.App.Services.Abstractions;

namespace Advertified.App.Services;

public sealed class MediaPlanningEngine : IMediaPlanningEngine
{
    private readonly IPlanningCandidateLoader _candidateLoader;
    private readonly IPlanningEligibilityService _eligibilityService;
    private readonly IRecommendationPlanBuilder _planBuilder;
    private readonly IRecommendationExplainabilityService _explainabilityService;

    public MediaPlanningEngine(
        IPlanningCandidateLoader candidateLoader,
        IPlanningEligibilityService eligibilityService,
        IRecommendationPlanBuilder planBuilder,
        IRecommendationExplainabilityService explainabilityService)
    {
        _candidateLoader = candidateLoader;
        _eligibilityService = eligibilityService;
        _planBuilder = planBuilder;
        _explainabilityService = explainabilityService;
    }

    public MediaPlanningEngine(IPlanningInventoryRepository repository, PlanningPolicySnapshotProvider snapshotProvider)
        : this(
            new PlanningCandidateLoader(repository),
            new PlanningEligibilityService(new PlanningPolicyService(snapshotProvider)),
            new RecommendationPlanBuilder(new PlanningPolicyService(snapshotProvider)),
            new RecommendationExplainabilityService(
                new PlanningScoreService(new PlanningPolicyService(snapshotProvider)),
                new PlanningPolicyService(snapshotProvider)))
    {
    }

    public async Task<RecommendationResult> GenerateAsync(CampaignPlanningRequest request, CancellationToken cancellationToken)
    {
        var allCandidates = await _candidateLoader.LoadCandidatesAsync(request, cancellationToken);
        var policyOutcome = _eligibilityService.FilterEligibleCandidates(allCandidates, request);
        var eligibleCandidates = policyOutcome.Candidates;

        foreach (var candidate in eligibleCandidates)
        {
            var analysis = _explainabilityService.AnalyzeCandidate(candidate, request);
            candidate.Score = analysis.Score;
            candidate.Metadata["selectionReasons"] = analysis.SelectionReasons;
            candidate.Metadata["policyFlags"] = analysis.PolicyFlags;
            candidate.Metadata["confidenceScore"] = analysis.ConfidenceScore;
        }

        var scored = eligibleCandidates
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Cost)
            .ToList();

        var basePlan = _planBuilder.BuildPlan(scored, request.SelectedBudget, request.MaxMediaItems, diversify: true);
        var recommendedPlan = _planBuilder.BuildPlan(scored, request.SelectedBudget, request.MaxMediaItems, diversify: false);

        var upsellBudget = request.OpenToUpsell
            ? request.SelectedBudget + (request.AdditionalBudget ?? 0m)
            : request.SelectedBudget;

        var upsells = request.OpenToUpsell && upsellBudget > request.SelectedBudget
            ? _planBuilder.BuildUpsells(scored, recommendedPlan, upsellBudget - recommendedPlan.Sum(x => x.TotalCost))
            : new List<PlannedItem>();

        var fallbackFlags = new List<string>(policyOutcome.FallbackFlags);
        if (eligibleCandidates.Count == 0)
        {
            fallbackFlags.Add("inventory_insufficient");
        }

        if (recommendedPlan.Count == 0)
        {
            fallbackFlags.Add("no_recommendation_generated");
        }

        fallbackFlags.AddRange(_explainabilityService.GetPreferredMediaFallbackFlags(request, recommendedPlan));

        return new RecommendationResult
        {
            BasePlan = basePlan,
            RecommendedPlan = recommendedPlan,
            Upsells = upsells,
            FallbackFlags = fallbackFlags.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            ManualReviewRequired = fallbackFlags.Count > 0,
            Rationale = _explainabilityService.BuildRationale(basePlan, recommendedPlan, request)
        };
    }
}
