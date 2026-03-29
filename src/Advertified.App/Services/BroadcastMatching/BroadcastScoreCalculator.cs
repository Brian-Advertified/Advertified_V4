namespace Advertified.App.Services.BroadcastMatching;

public sealed class BroadcastScoreCalculator : IBroadcastScoreCalculator
{
    private readonly BroadcastMatcherPolicy _policy;

    public BroadcastScoreCalculator(BroadcastMatcherPolicy policy)
    {
        _policy = policy;
    }

    public BroadcastMatchCandidate Score(
        BroadcastMediaOutlet outlet,
        BroadcastMatchRequest request,
        IReadOnlyList<BroadcastMediaOutlet> eligiblePool,
        BroadcastMatchingMode mode)
    {
        var matchedDimensions = BuildMatchedDimensions(outlet, request);
        var breakdown = new BroadcastScoreBreakdown
        {
            GeographyScore = ScoreGeography(outlet, request),
            AudienceScore = ScoreAudience(outlet, request),
            LanguageScore = ScoreLanguage(outlet, request),
            PricingScore = ScorePricing(outlet, request),
            ListenershipScore = ScoreListenership(outlet, eligiblePool),
            MediaTypeScore = request.RequestedMediaTypes.Count > 0 && request.RequestedMediaTypes.Contains(outlet.MediaType)
                ? _policy.MediaTypeWeight
                : 0m,
            CoverageScore = ScoreCoverage(outlet, request),
            CatalogHealthScore = ScoreCatalogHealth(outlet.CatalogHealth)
        };

        breakdown.BoostsScore = ScoreBoosts(outlet, request, eligiblePool);
        breakdown.PenaltiesScore = ScorePenalties(outlet, request, mode);

        var finalScore = BroadcastMatchingHelpers.ClampScore(
            breakdown.WeightedScore + breakdown.BoostsScore - breakdown.PenaltiesScore);

        return new BroadcastMatchCandidate
        {
            Outlet = outlet,
            Breakdown = breakdown,
            MatchedDimensions = matchedDimensions,
            Flags = BuildFlags(outlet),
            ReasoningSummary = BuildReasoningSummary(outlet, matchedDimensions, finalScore),
            FinalScore = decimal.Round(finalScore, 2, MidpointRounding.AwayFromZero),
            RecommendationTier = GetTier(finalScore)
        };
    }

    private decimal ScoreGeography(BroadcastMediaOutlet outlet, BroadcastMatchRequest request)
    {
        if (request.TargetProvinceCodes.Count == 0 && request.TargetCityLabels.Count == 0)
        {
            return 0m;
        }

        var provinceScore = 0m;
        if (request.TargetProvinceCodes.Count > 0)
        {
            var matched = BroadcastMatchingHelpers.OverlapCount(request.TargetProvinceCodes, outlet.ProvinceCodes);
            provinceScore = _policy.ProvinceOverlapWeight
                * BroadcastMatchingHelpers.Ratio(matched, request.TargetProvinceCodes.Count);
        }

        var cityScore = 0m;
        if (request.TargetCityLabels.Count > 0)
        {
            var matched = BroadcastMatchingHelpers.OverlapCount(request.TargetCityLabels, outlet.CityNames);
            cityScore = _policy.CityOverlapWeight
                * BroadcastMatchingHelpers.Ratio(matched, request.TargetCityLabels.Count);
        }

        var nationalBonus = outlet.IsNational
            && (request.TargetProvinceCodes.Count > 1 || request.TargetProvinceCodes.Count == 0)
            ? _policy.NationalBonusWeight
            : 0m;

        return provinceScore + cityScore + nationalBonus;
    }

    private decimal ScoreAudience(BroadcastMediaOutlet outlet, BroadcastMatchRequest request)
    {
        var ageScore = ScoreAge(outlet.AudienceAgeSkew, request.TargetAgeSkew);
        var genderScore = BroadcastMatchingHelpers.EqualsText(outlet.AudienceGenderSkew, request.TargetGenderSkew)
            ? _policy.GenderMatchWeight
            : 0m;
        var lsmScore = BroadcastMatchingHelpers.RangeOverlaps(outlet.AudienceLsmRange, request.TargetLsmRange)
            ? _policy.LsmMatchWeight
            : 0m;
        var racialScore = BroadcastMatchingHelpers.EqualsText(outlet.AudienceRacialSkew, request.TargetRacialSkew)
            ? _policy.RacialMatchWeight
            : 0m;
        var urbanRuralScore = ScoreUrbanRural(outlet.UrbanRuralMix, request.TargetUrbanRural);
        var keywordScore = 0m;

        if (request.TargetKeywords.Count > 0)
        {
            var matchedKeywords = BroadcastMatchingHelpers.OverlapCount(request.TargetKeywords, outlet.Keywords);
            keywordScore = _policy.KeywordMatchWeight
                * BroadcastMatchingHelpers.Ratio(matchedKeywords, request.TargetKeywords.Count);
        }

        return ageScore + genderScore + lsmScore + racialScore + urbanRuralScore + keywordScore;
    }

    private decimal ScoreAge(string? outletAge, string? requestAge)
    {
        if (BroadcastMatchingHelpers.EqualsText(outletAge, requestAge))
        {
            return _policy.AgeMatchWeight;
        }

        if (string.IsNullOrWhiteSpace(outletAge) || string.IsNullOrWhiteSpace(requestAge))
        {
            return 0m;
        }

        return BroadcastMatchingHelpers.RangeOverlaps(outletAge, requestAge)
            ? decimal.Round(_policy.AgeMatchWeight * 0.6m, 2, MidpointRounding.AwayFromZero)
            : 0m;
    }

    private decimal ScoreUrbanRural(BroadcastUrbanRuralMix outletMix, BroadcastUrbanRuralMix? requestedMix)
    {
        if (!requestedMix.HasValue || outletMix == BroadcastUrbanRuralMix.Unknown)
        {
            return 0m;
        }

        if (outletMix == requestedMix.Value)
        {
            return _policy.UrbanRuralMatchWeight;
        }

        if (outletMix == BroadcastUrbanRuralMix.Mixed)
        {
            return decimal.Round(_policy.UrbanRuralMatchWeight * 0.67m, 2, MidpointRounding.AwayFromZero);
        }

        return 0m;
    }

    private decimal ScoreLanguage(BroadcastMediaOutlet outlet, BroadcastMatchRequest request)
    {
        if (request.TargetLanguages.Count == 0)
        {
            return 0m;
        }

        var primaryScore = BroadcastMatchingHelpers.HasOverlap(request.TargetLanguages, outlet.PrimaryLanguages)
            ? _policy.PrimaryLanguageWeight
            : 0m;
        var notesScore = BroadcastMatchingHelpers.ContainsAnyText(outlet.LanguageNotes, request.TargetLanguages)
            ? _policy.LanguageNotesWeight
            : 0m;

        return primaryScore + notesScore;
    }

    private decimal ScorePricing(BroadcastMediaOutlet outlet, BroadcastMatchRequest request)
    {
        if (!outlet.HasPricing)
        {
            return 0m;
        }

        var minimumPrice = BroadcastMatchingHelpers.GetMinimumPrice(outlet);
        if (!minimumPrice.HasValue)
        {
            return 0m;
        }

        var budgetScore = ScoreBudgetFit(minimumPrice.Value, request.BudgetMaxZar);
        var slotDensityScore = Math.Min(_policy.SlotDensityWeight, Math.Max(0m, outlet.PricePointsZar.Count / 5m));
        var packagePresenceScore = outlet.HasPackagePricing
            ? _policy.PackagePresenceWeight
            : outlet.PricePointsZar.Count > 1
                ? decimal.Round(_policy.PackagePresenceWeight * 0.5m, 2, MidpointRounding.AwayFromZero)
                : 0m;

        return budgetScore + slotDensityScore + packagePresenceScore;
    }

    private decimal ScoreBudgetFit(decimal minimumPrice, decimal? budgetMax)
    {
        if (!budgetMax.HasValue)
        {
            return _policy.BudgetFitWeight;
        }

        if (minimumPrice <= budgetMax.Value)
        {
            return _policy.BudgetFitWeight;
        }

        if (minimumPrice <= budgetMax.Value * 1.15m)
        {
            return decimal.Round(_policy.BudgetFitWeight * 0.6m, 2, MidpointRounding.AwayFromZero);
        }

        if (minimumPrice <= budgetMax.Value * 1.30m)
        {
            return decimal.Round(_policy.BudgetFitWeight * 0.3m, 2, MidpointRounding.AwayFromZero);
        }

        return 0m;
    }

    private decimal ScoreListenership(BroadcastMediaOutlet outlet, IReadOnlyList<BroadcastMediaOutlet> eligiblePool)
    {
        var weeklyValues = eligiblePool
            .Where(static item => item.ListenershipWeekly.HasValue)
            .Select(static item => item.ListenershipWeekly!.Value)
            .OrderBy(static item => item)
            .ToArray();
        var dailyValues = eligiblePool
            .Where(static item => item.ListenershipDaily.HasValue)
            .Select(static item => item.ListenershipDaily!.Value)
            .OrderBy(static item => item)
            .ToArray();

        var weeklyScore = outlet.ListenershipWeekly.HasValue
            ? _policy.WeeklyListenershipWeight * BroadcastMatchingHelpers.PercentileRank(outlet.ListenershipWeekly.Value, weeklyValues)
            : 0m;
        var dailyScore = outlet.ListenershipDaily.HasValue
            ? _policy.DailyListenershipWeight * BroadcastMatchingHelpers.PercentileRank(outlet.ListenershipDaily.Value, dailyValues)
            : 0m;

        return weeklyScore + dailyScore;
    }

    private decimal ScoreCoverage(BroadcastMediaOutlet outlet, BroadcastMatchRequest request)
    {
        if (!request.RequestedCoverageType.HasValue)
        {
            return 0m;
        }

        if (outlet.CoverageType == request.RequestedCoverageType.Value)
        {
            return _policy.CoverageWeight;
        }

        if (request.RequestedCoverageType == BroadcastCoverageType.Regional && outlet.CoverageType == BroadcastCoverageType.National)
        {
            return 3m;
        }

        if (request.RequestedCoverageType == BroadcastCoverageType.Local && outlet.CoverageType == BroadcastCoverageType.Regional)
        {
            return 2m;
        }

        return 0m;
    }

    private decimal ScoreCatalogHealth(BroadcastCatalogHealth health) => health switch
    {
        BroadcastCatalogHealth.Strong => _policy.StrongCatalogHealthScore,
        BroadcastCatalogHealth.MixedNotFullyHealthy => _policy.MixedCatalogHealthScore,
        BroadcastCatalogHealth.WeakUnpriced => _policy.WeakUnpricedCatalogHealthScore,
        _ => 0m
    };

    private decimal ScoreBoosts(
        BroadcastMediaOutlet outlet,
        BroadcastMatchRequest request,
        IReadOnlyList<BroadcastMediaOutlet> eligiblePool)
    {
        var boosts = 0m;

        if (BroadcastMatchingHelpers.HasOverlap(request.TargetProvinceCodes, outlet.ProvinceCodes))
        {
            boosts += _policy.ProvinceExactBoost;
        }

        if (BroadcastMatchingHelpers.HasOverlap(request.TargetCityLabels, outlet.CityNames))
        {
            boosts += _policy.CityExactBoost;
        }

        if (BroadcastMatchingHelpers.HasOverlap(request.TargetLanguages, outlet.PrimaryLanguages))
        {
            boosts += _policy.LanguageExactBoost;
        }

        if (request.TargetKeywords.Count > 0)
        {
            var overlap = BroadcastMatchingHelpers.OverlapCount(request.TargetKeywords, outlet.Keywords);
            if (overlap >= Math.Max(2, request.TargetKeywords.Count / 2))
            {
                boosts += _policy.KeywordHighOverlapBoost;
            }
        }

        if (outlet.IsNational && request.TargetProvinceCodes.Count > 1)
        {
            boosts += _policy.NationalMultiProvinceBoost;
        }

        if (IsTopQuartile(outlet, eligiblePool))
        {
            boosts += _policy.HighListenershipBoost;
        }

        if (outlet.HasPackagePricing && outlet.HasSlotRatePricing)
        {
            boosts += _policy.DirectPricingBoost;
        }

        return Math.Min(_policy.MaxBoosts, boosts);
    }

    private decimal ScorePenalties(BroadcastMediaOutlet outlet, BroadcastMatchRequest request, BroadcastMatchingMode mode)
    {
        var penalties = 0m;
        var budgetActive = request.BudgetMinZar.HasValue || request.BudgetMaxZar.HasValue;

        if (budgetActive && !outlet.HasPricing)
        {
            penalties += _policy.MissingPricingPenalty;
        }

        if (outlet.ProvinceCodes.Count == 0 && outlet.CityNames.Count == 0)
        {
            penalties += _policy.MissingGeographyPenalty;
        }

        if (outlet.Keywords.Count == 0)
        {
            penalties += _policy.MissingAudienceKeywordsPenalty;
        }

        if (!outlet.ListenershipWeekly.HasValue && !outlet.ListenershipDaily.HasValue)
        {
            penalties += _policy.MissingListenershipPenalty;
        }

        if (outlet.CatalogHealth == BroadcastCatalogHealth.WeakUnpriced)
        {
            penalties += _policy.WeakUnpricedPenalty;
        }

        if (outlet.CatalogHealth == BroadcastCatalogHealth.MixedNotFullyHealthy)
        {
            penalties += _policy.MixedHealthPenalty;
        }

        if (!string.IsNullOrWhiteSpace(outlet.DataSourceEnrichment)
            && outlet.DataSourceEnrichment.Contains("undated", StringComparison.OrdinalIgnoreCase))
        {
            penalties += _policy.StaleEnrichmentPenalty;
        }

        if (mode == BroadcastMatchingMode.GeographyFirstRescueMode
            && request.TargetCityLabels.Count > 0
            && outlet.ProvinceCodes.Count > 0
            && !BroadcastMatchingHelpers.HasOverlap(request.TargetCityLabels, outlet.CityNames))
        {
            penalties += _policy.RescueCityPenalty;
        }

        return penalties;
    }

    private static BroadcastMatchedDimensions BuildMatchedDimensions(BroadcastMediaOutlet outlet, BroadcastMatchRequest request) =>
        new()
        {
            Geography = request.TargetProvinceCodes
                .Intersect(outlet.ProvinceCodes, StringComparer.OrdinalIgnoreCase)
                .Concat(request.TargetCityLabels.Intersect(outlet.CityNames, StringComparer.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList(),
            Languages = request.TargetLanguages
                .Intersect(outlet.PrimaryLanguages, StringComparer.OrdinalIgnoreCase)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList(),
            AudienceKeywords = request.TargetKeywords
                .Intersect(outlet.Keywords, StringComparer.OrdinalIgnoreCase)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList()
        };

    private static List<string> BuildFlags(BroadcastMediaOutlet outlet)
    {
        var flags = outlet.DataQualityFlags
            .Where(static flag => !string.IsNullOrWhiteSpace(flag))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static flag => flag, StringComparer.OrdinalIgnoreCase)
            .ToList();
        flags.Add(BroadcastMatchingHelpers.HealthFlag(outlet.CatalogHealth));
        if (outlet.HasPricing)
        {
            flags.Add("has_pricing");
        }

        return flags.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private string BuildReasoningSummary(
        BroadcastMediaOutlet outlet,
        BroadcastMatchedDimensions matchedDimensions,
        decimal finalScore)
    {
        var reasons = new List<string>();

        if (matchedDimensions.Geography.Count > 0)
        {
            reasons.Add("strong geography fit");
        }

        if (matchedDimensions.Languages.Count > 0)
        {
            reasons.Add("aligned language profile");
        }

        if (matchedDimensions.AudienceKeywords.Count > 0)
        {
            reasons.Add("audience keyword overlap");
        }

        if (outlet.HasPricing)
        {
            reasons.Add("usable pricing");
        }

        reasons.Add(outlet.CatalogHealth switch
        {
            BroadcastCatalogHealth.Strong => "healthy catalog status",
            BroadcastCatalogHealth.MixedNotFullyHealthy => "mixed operational health",
            BroadcastCatalogHealth.WeakUnpriced => "pricing weakness present",
            BroadcastCatalogHealth.WeakNoInventory => "inventory weakness present",
            BroadcastCatalogHealth.WeakPartialPricing => "partial pricing risk",
            _ => "catalog uncertainty present"
        });

        return $"{outlet.Name} scored {BroadcastMatchingHelpers.FormatScore(finalScore)} because of {string.Join(", ", reasons)}.";
    }

    private bool IsTopQuartile(BroadcastMediaOutlet outlet, IReadOnlyList<BroadcastMediaOutlet> eligiblePool)
    {
        if (!outlet.ListenershipWeekly.HasValue)
        {
            return false;
        }

        var weeklyValues = eligiblePool
            .Where(static item => item.ListenershipWeekly.HasValue)
            .Select(static item => item.ListenershipWeekly!.Value)
            .OrderBy(static item => item)
            .ToArray();

        return weeklyValues.Length > 0
            && BroadcastMatchingHelpers.PercentileRank(outlet.ListenershipWeekly.Value, weeklyValues) >= 0.75m;
    }

    private BroadcastRecommendationTier GetTier(decimal finalScore)
    {
        if (finalScore >= _policy.PremiumThreshold)
        {
            return BroadcastRecommendationTier.Premium;
        }

        if (finalScore >= _policy.RecommendedThreshold)
        {
            return BroadcastRecommendationTier.Recommended;
        }

        if (finalScore >= _policy.EligibleThreshold)
        {
            return BroadcastRecommendationTier.Eligible;
        }

        return BroadcastRecommendationTier.NotEligible;
    }
}
