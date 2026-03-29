using Advertified.App.Services.Abstractions;

namespace Advertified.App.Services.BroadcastMatching;

public interface IBroadcastMatchRequestNormalizer
{
    void Normalize(BroadcastMatchRequest request);
}

public interface IBroadcastMatchRequestValidator
{
    void Validate(BroadcastMatchRequest request);
}

public interface IBroadcastHardFilterEngine
{
    string? GetExclusionReason(BroadcastMediaOutlet outlet, BroadcastMatchRequest request, BroadcastMatchingMode mode);
}

public interface IBroadcastScoreCalculator
{
    BroadcastMatchCandidate Score(
        BroadcastMediaOutlet outlet,
        BroadcastMatchRequest request,
        IReadOnlyList<BroadcastMediaOutlet> eligiblePool,
        BroadcastMatchingMode mode);
}

public interface IBroadcastRecommendationRanker
{
    List<BroadcastMatchCandidate> Rank(IReadOnlyList<BroadcastMatchCandidate> candidates, BroadcastMatchRequest request);
}

public sealed class BroadcastMatchRequestNormalizer : IBroadcastMatchRequestNormalizer
{
    public void Normalize(BroadcastMatchRequest request)
    {
        request.TargetProvinceCodes = BroadcastMatchingHelpers.NormalizeTokens(request.TargetProvinceCodes);
        request.TargetCityLabels = BroadcastMatchingHelpers.NormalizeLabels(request.TargetCityLabels);
        request.TargetLanguages = BroadcastMatchingHelpers.NormalizeTokens(request.TargetLanguages);
        request.TargetKeywords = BroadcastMatchingHelpers.NormalizeTokens(request.TargetKeywords);
    }
}

public sealed class BroadcastMatchRequestValidator : IBroadcastMatchRequestValidator
{
    public void Validate(BroadcastMatchRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.CampaignId))
        {
            throw new ArgumentException("CampaignId is required.", nameof(request));
        }

        if (request.RequestedMediaTypes.Count == 0)
        {
            throw new ArgumentException("At least one requested media type is required.", nameof(request));
        }

        if (request.TargetProvinceCodes.Count == 0)
        {
            throw new ArgumentException("At least one target province code is required.", nameof(request));
        }

        if (!request.BudgetMinZar.HasValue || !request.BudgetMaxZar.HasValue)
        {
            throw new ArgumentException("BudgetMinZar and BudgetMaxZar are required.", nameof(request));
        }

        if (request.BudgetMinZar < 0 || request.BudgetMaxZar < 0)
        {
            throw new ArgumentException("Budgets cannot be negative.", nameof(request));
        }

        if (request.BudgetMinZar > request.BudgetMaxZar)
        {
            throw new ArgumentException("BudgetMinZar must be less than or equal to BudgetMaxZar.", nameof(request));
        }
    }
}

public sealed class BroadcastHardFilterEngine : IBroadcastHardFilterEngine
{
    public string? GetExclusionReason(BroadcastMediaOutlet outlet, BroadcastMatchRequest request, BroadcastMatchingMode mode)
    {
        var budgetActive = request.BudgetMinZar.HasValue || request.BudgetMaxZar.HasValue;
        var geographyRequested = request.TargetProvinceCodes.Count > 0 || request.TargetCityLabels.Count > 0;
        var strictLike = mode is BroadcastMatchingMode.StrictFilterThenScore or BroadcastMatchingMode.GeographyFirstRescueMode;

        if (request.RequestedMediaTypes.Count > 0 && !request.RequestedMediaTypes.Contains(outlet.MediaType))
        {
            return "media_type_mismatch";
        }

        if (outlet.CatalogHealth == BroadcastCatalogHealth.WeakNoInventory)
        {
            return "weak_no_inventory";
        }

        if (budgetActive && !outlet.HasPricing && strictLike)
        {
            return "missing_pricing";
        }

        if (geographyRequested && !HasGeographyOverlap(outlet, request, mode))
        {
            return "no_geography_overlap";
        }

        if (request.MinWeeklyListenership.HasValue)
        {
            if (outlet.ListenershipWeekly.HasValue)
            {
                if (outlet.ListenershipWeekly.Value < request.MinWeeklyListenership.Value)
                {
                    return "below_min_weekly_listenership";
                }
            }
            else if (strictLike)
            {
                return "missing_weekly_listenership";
            }
        }

        if (request.MinDailyListenership.HasValue)
        {
            if (outlet.ListenershipDaily.HasValue)
            {
                if (outlet.ListenershipDaily.Value < request.MinDailyListenership.Value)
                {
                    return "below_min_daily_listenership";
                }
            }
            else if (strictLike)
            {
                return "missing_daily_listenership";
            }
        }

        if (mode == BroadcastMatchingMode.GeographyFirstRescueMode
            && outlet.CatalogHealth == BroadcastCatalogHealth.WeakUnpriced
            && !outlet.HasPricing)
        {
            return "weak_unpriced_without_pricing";
        }

        return null;
    }

    private static bool HasGeographyOverlap(
        BroadcastMediaOutlet outlet,
        BroadcastMatchRequest request,
        BroadcastMatchingMode mode)
    {
        var provinceMatch = BroadcastMatchingHelpers.HasOverlap(request.TargetProvinceCodes, outlet.ProvinceCodes);
        var cityMatch = BroadcastMatchingHelpers.HasOverlap(request.TargetCityLabels, outlet.CityNames);
        var nationalPass = outlet.IsNational
            && (request.TargetProvinceCodes.Count > 1
                || mode == BroadcastMatchingMode.SoftScoreWithPenalties
                || mode == BroadcastMatchingMode.GeographyFirstRescueMode);

        return provinceMatch || cityMatch || nationalPass;
    }
}

public sealed class BroadcastRecommendationRanker : IBroadcastRecommendationRanker
{
    public List<BroadcastMatchCandidate> Rank(IReadOnlyList<BroadcastMatchCandidate> candidates, BroadcastMatchRequest request)
    {
        var ranked = candidates
            .OrderByDescending(candidate => candidate.FinalScore)
            .ThenByDescending(candidate => candidate.Outlet.HasPricing)
            .ThenBy(candidate => candidate.Outlet.CatalogHealth)
            .ThenByDescending(candidate => BroadcastMatchingHelpers.OverlapCount(request.TargetProvinceCodes, candidate.Outlet.ProvinceCodes))
            .ThenByDescending(candidate => candidate.Outlet.ListenershipWeekly ?? -1)
            .ThenByDescending(candidate => candidate.Outlet.ListenershipDaily ?? -1)
            .ThenBy(candidate => candidate.Outlet.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        for (var index = 0; index < ranked.Count; index++)
        {
            ranked[index].RankPosition = index + 1;
        }

        return ranked;
    }
}

public sealed class BroadcastMatchingEngine : IBroadcastMatchingEngine
{
    private readonly IBroadcastMatchRequestNormalizer _normalizer;
    private readonly IBroadcastMatchRequestValidator _validator;
    private readonly IBroadcastHardFilterEngine _hardFilterEngine;
    private readonly IBroadcastScoreCalculator _scoreCalculator;
    private readonly IBroadcastRecommendationRanker _ranker;

    public BroadcastMatchingEngine(
        IBroadcastMatchRequestNormalizer normalizer,
        IBroadcastMatchRequestValidator validator,
        IBroadcastHardFilterEngine hardFilterEngine,
        IBroadcastScoreCalculator scoreCalculator,
        IBroadcastRecommendationRanker ranker)
    {
        _normalizer = normalizer;
        _validator = validator;
        _hardFilterEngine = hardFilterEngine;
        _scoreCalculator = scoreCalculator;
        _ranker = ranker;
    }

    public BroadcastMatchResponse Match(IEnumerable<BroadcastMediaOutlet> outlets, BroadcastMatchRequest request)
    {
        ArgumentNullException.ThrowIfNull(outlets);
        ArgumentNullException.ThrowIfNull(request);

        _normalizer.Normalize(request);
        _validator.Validate(request);

        var allOutlets = outlets.ToList();
        var strict = ExecuteMode(allOutlets, request, BroadcastMatchingMode.StrictFilterThenScore);
        if (strict.Results.Count > 0)
        {
            return strict;
        }

        var soft = ExecuteMode(allOutlets, request, BroadcastMatchingMode.SoftScoreWithPenalties);
        if (soft.Results.Count > 0)
        {
            soft.Warnings.Add("No results passed strict mode. Returned best available options using soft-score mode.");
            return soft;
        }

        var rescue = ExecuteMode(allOutlets, request, BroadcastMatchingMode.GeographyFirstRescueMode);
        rescue.Warnings.Add(
            rescue.Results.Count == 0
                ? "No fully qualified stations met all campaign constraints."
                : "No fully qualified stations met all campaign constraints. Returned best available geography-aligned options.");
        return rescue;
    }

    private BroadcastMatchResponse ExecuteMode(
        IReadOnlyList<BroadcastMediaOutlet> allOutlets,
        BroadcastMatchRequest request,
        BroadcastMatchingMode mode)
    {
        var excludedCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var filtered = new List<BroadcastMediaOutlet>();

        foreach (var outlet in allOutlets)
        {
            var exclusionReason = _hardFilterEngine.GetExclusionReason(outlet, request, mode);
            if (string.IsNullOrWhiteSpace(exclusionReason))
            {
                filtered.Add(outlet);
                continue;
            }

            excludedCounts.TryGetValue(exclusionReason, out var count);
            excludedCounts[exclusionReason] = count + 1;
        }

        var scored = filtered
            .Select(outlet => _scoreCalculator.Score(outlet, request, filtered, mode))
            .ToList();

        if (mode == BroadcastMatchingMode.StrictFilterThenScore)
        {
            scored = scored
                .Where(candidate => candidate.RecommendationTier != BroadcastRecommendationTier.NotEligible)
                .ToList();
        }

        var ranked = _ranker.Rank(scored, request);

        return new BroadcastMatchResponse
        {
            CampaignId = request.CampaignId,
            ModeUsed = mode,
            TotalCandidatesScanned = allOutlets.Count,
            EligibleCandidates = ranked.Count,
            RecommendedCandidates = ranked.Count(candidate => candidate.RecommendationTier is BroadcastRecommendationTier.Recommended or BroadcastRecommendationTier.Premium),
            Results = ranked,
            ExcludedCounts = excludedCounts
        };
    }
}
