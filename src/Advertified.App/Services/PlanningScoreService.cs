using Advertified.App.Contracts.Campaigns;
using Advertified.App.Domain.Campaigns;
using Advertified.App.Services.Abstractions;
using Advertified.App.Support;
using System.Text.RegularExpressions;
using System.Text.Json;

namespace Advertified.App.Services;

public sealed class PlanningScoreService : IPlanningScoreService
{
    private const decimal BroadcastPrimaryLanguageMatchScore = 32m;
    private const decimal BroadcastSecondaryLanguageMatchScore = 20m;
    private const decimal BroadcastLanguageMismatchPenalty = -24m;
    private const decimal BroadcastSuburbTokenBonus = 18m;
    private readonly IPlanningPolicyService _policyService;
    private readonly IBroadcastMasterDataService _broadcastMasterDataService;
    private readonly ILeadMasterDataService _leadMasterDataService;
    private readonly IIndustryArchetypeScoringService _industryArchetypeScoringService;

    public PlanningScoreService(
        IPlanningPolicyService policyService,
        IBroadcastMasterDataService broadcastMasterDataService,
        ILeadMasterDataService leadMasterDataService,
        IIndustryArchetypeScoringService industryArchetypeScoringService)
    {
        _policyService = policyService;
        _broadcastMasterDataService = broadcastMasterDataService;
        _leadMasterDataService = leadMasterDataService;
        _industryArchetypeScoringService = industryArchetypeScoringService;
    }

    public PlanningScoreService(IPlanningPolicyService policyService, IBroadcastMasterDataService broadcastMasterDataService)
        : this(policyService, broadcastMasterDataService, new NoOpLeadMasterDataService(), new NoOpIndustryArchetypeScoringService())
    {
    }

    public PlanningScoreService(
        IPlanningPolicyService policyService,
        IBroadcastMasterDataService broadcastMasterDataService,
        ILeadMasterDataService leadMasterDataService)
        : this(policyService, broadcastMasterDataService, leadMasterDataService, new NoOpIndustryArchetypeScoringService())
    {
    }

    public PlanningCandidateAnalysis AnalyzeCandidate(InventoryCandidate candidate, CampaignPlanningRequest request)
    {
        return new PlanningCandidateAnalysis(ScoreCandidate(candidate, request), Array.Empty<string>(), Array.Empty<string>(), 0m);
    }

    public decimal GeoScore(InventoryCandidate candidate, CampaignPlanningRequest request)
    {
        var scope = NormalizeScope(request.GeographyScope);
        var candidateCoverage = ResolveCandidateCoverage(candidate);
        var isBroadcast = candidate.MediaType.Equals("Radio", StringComparison.OrdinalIgnoreCase)
            || candidate.MediaType.Equals("TV", StringComparison.OrdinalIgnoreCase);

        var score = scope switch
        {
            "national" => candidateCoverage switch
            {
                "national" => 22m,
                "provincial" => 12m,
                "local" => 6m,
                _ => 8m
            },
            "provincial" => candidateCoverage switch
            {
                "provincial" => 22m,
                "local" => 14m,
                "national" => 10m,
                _ => 8m
            },
            "local" => candidateCoverage switch
            {
                "local" => 22m,
                "provincial" => 12m,
                "national" => 8m,
                _ => 8m
            },
            _ => 8m
        };

        if (request.Suburbs.Any(x => MatchesGeo(x, candidate.Suburb) || MatchesGeo(x, candidate.Area)))
        {
            score += 10m;
        }

        // Broadcast inventory isn't sold at suburb precision, but our master data often carries
        // meaningful neighborhood/city labels (e.g. "Soweto") in `cityLabels`. When the user
        // selects a suburb, extract its tokens and boost candidates whose labels include them.
        // If no station matches the token, nothing breaks: all other geo scoring still applies.
        if (isBroadcast && request.Suburbs.Count > 0)
        {
            var suburbTokens = ExtractSuburbTokens(request.Suburbs);
            // Important: use exact token matching here (no geography aliasing), otherwise a token like
            // "Soweto" could alias to "Johannesburg" and incorrectly boost stations that only list
            // the broader city label.
            if (suburbTokens.Length > 0 && suburbTokens.Any(token =>
                    MatchesAnyMetadataTokenExact(candidate, token, "cityLabels", "city_labels", "city", "area")))
            {
                score += 10m;
            }
        }

        if (request.Cities.Any(x =>
            MatchesGeo(x, candidate.City)
            || MatchesAnyMetadataToken(candidate, x, "cityLabels", "city_labels", "city", "area")))
        {
            score += 10m;
        }

        if (request.Provinces.Any(x =>
            MatchesGeo(x, candidate.Province)
            || MatchesAnyMetadataToken(candidate, x, "provinceCodes", "province_codes", "province", "area")))
        {
            score += 10m;
        }

        if (request.Areas.Any(x => MatchesGeo(x, candidate.Area) || MatchesGeo(x, candidate.Suburb)))
        {
            score += 8m;
        }

        return Math.Min(36m, score);
    }

    private static string[] ExtractSuburbTokens(IEnumerable<string> suburbs)
    {
        return suburbs
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .SelectMany(value => value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static bool MatchesAnyMetadataTokenExact(InventoryCandidate candidate, string requestedValue, params string[] keys)
    {
        if (string.IsNullOrWhiteSpace(requestedValue) || candidate.Metadata.Count == 0)
        {
            return false;
        }

        return keys.Any(key =>
            candidate.Metadata.TryGetValue(key, out var value)
            && ExtractMetadataTokens(value).Any(token => Matches(requestedValue, token)));
    }

    public decimal AudienceScore(InventoryCandidate candidate, CampaignPlanningRequest request)
    {
        decimal score = 0m;
        score += LanguageScore(candidate, request);

        if (request.TargetLsmMin.HasValue && request.TargetLsmMax.HasValue && candidate.LsmMin.HasValue && candidate.LsmMax.HasValue)
        {
            var overlap = !(candidate.LsmMax.Value < request.TargetLsmMin.Value || candidate.LsmMin.Value > request.TargetLsmMax.Value);
            if (overlap)
            {
                score += 15m;
            }
        }

        score += AgeScore(candidate, request);
        score += GenderScore(candidate, request);
        score += AudienceKeywordScore(candidate, request);

        return score;
    }

    private decimal LanguageScore(InventoryCandidate candidate, CampaignPlanningRequest request)
    {
        if (request.TargetLanguages.Count == 0)
        {
            return 0m;
        }

        var requested = request.TargetLanguages
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(_broadcastMasterDataService.NormalizeLanguageForMatching)
            .Where(static value => value.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (requested.Length == 0)
        {
            return 0m;
        }

        var candidateLanguages = BroadcastLanguageSupport.ExtractCandidateLanguageCodes(candidate, _broadcastMasterDataService.NormalizeLanguageCode);
        var noteMatches = requested
            .Where(value => MatchesAnyMetadataToken(candidate, value, "languageNotes", "language_notes", "targetAudience", "target_audience"))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var candidateMatches = requested
            .Where(value => candidateLanguages.Contains(value, StringComparer.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (candidate.MediaType.Equals("Radio", StringComparison.OrdinalIgnoreCase)
            || candidate.MediaType.Equals("TV", StringComparison.OrdinalIgnoreCase))
        {
            if (candidateMatches.Length == 0 && noteMatches.Length == 0)
            {
                return BroadcastLanguageMismatchPenalty;
            }

            decimal score = 0m;
            for (var index = 0; index < requested.Length; index++)
            {
                var language = requested[index];
                if (candidateMatches.Contains(language, StringComparer.OrdinalIgnoreCase))
                {
                    score += index switch
                    {
                        0 => 32m,
                        1 => 22m,
                        2 => 16m,
                        3 => 12m,
                        _ => 8m
                    };
                    continue;
                }

                if (noteMatches.Contains(language, StringComparer.OrdinalIgnoreCase))
                {
                    score += index switch
                    {
                        0 => 20m,
                        1 => 14m,
                        2 => 10m,
                        3 => 8m,
                        _ => 5m
                    };
                }
            }

            if (candidateMatches.Length == requested.Length && requested.Length > 1)
            {
                score += 18m;
            }
            else if (candidateMatches.Length > 1)
            {
                score += 8m;
            }

            return Math.Max(BroadcastLanguageMismatchPenalty, Math.Min(54m, score));
        }

        var hasDirectMatch = candidateMatches.Length > 0;
        var hasLanguageNotesMatch = noteMatches.Length > 0;

        if (hasDirectMatch)
        {
            return 10m;
        }

        return hasLanguageNotesMatch ? 6m : 0m;
    }

    public decimal BudgetScore(InventoryCandidate candidate, CampaignPlanningRequest request)
    {
        var comparableCost = GetComparableMonthlyCost(candidate);
        if (comparableCost <= 0 || request.SelectedBudget <= 0)
        {
            return 0m;
        }

        var ratio = comparableCost / request.SelectedBudget;

        if (ratio <= 0.15m) return 20m;
        if (ratio <= 0.30m) return 16m;
        if (ratio <= 0.50m) return 12m;
        if (ratio <= 0.80m) return 8m;
        if (ratio <= 1.00m) return 4m;
        return 0m;
    }

    public decimal MediaPreferenceScore(InventoryCandidate candidate, CampaignPlanningRequest request)
    {
        if (request.PreferredMediaTypes.Count == 0) return 6m;
        return request.PreferredMediaTypes.Any(x => Matches(x, candidate.MediaType) || Matches(x, candidate.Subtype))
            ? 15m
            : 0m;
    }

    public decimal MixTargetScore(InventoryCandidate candidate, CampaignPlanningRequest request)
    {
        var share = _policyService.GetTargetShare(candidate.MediaType, request);
        if (!share.HasValue) return 0m;
        if (share.Value <= 0) return -12m;
        if (share.Value >= 60) return 24m;
        if (share.Value >= 40) return 16m;
        if (share.Value >= 20) return 8m;
        return 3m;
    }

    public decimal IndustryContextFitScore(InventoryCandidate candidate, CampaignPlanningRequest request)
    {
        var archetypes = ResolveIndustryArchetypes(request);
        if (archetypes.Count == 0)
        {
            return 0m;
        }

        var score = 0m;
        foreach (var archetype in archetypes)
        {
            score += ScoreArchetypeMediaFit(archetype, candidate);
            score += ScoreArchetypeMetadataFit(archetype, candidate);
        }

        score += ScoreAudienceRequestFit(candidate, request);
        score += ScoreGenderRequestFit(candidate, request);

        return Math.Min(14m, Math.Max(-4m, score));
    }

    private decimal ScoreCandidate(InventoryCandidate candidate, CampaignPlanningRequest request)
    {
        decimal score = 0m;

        score += GeoScore(candidate, request);

        // Extra boost outside `GeoScore` (which is capped) so suburb tokens like "Soweto" can
        // decisively prefer truly local/community stations when available.
        if ((candidate.MediaType.Equals("Radio", StringComparison.OrdinalIgnoreCase)
                || candidate.MediaType.Equals("TV", StringComparison.OrdinalIgnoreCase))
            && request.Suburbs.Count > 0)
        {
            var suburbTokens = ExtractSuburbTokens(request.Suburbs);
            if (suburbTokens.Length > 0 && suburbTokens.Any(token =>
                    MatchesAnyMetadataTokenExact(candidate, token, "cityLabels", "city_labels", "city", "area")))
            {
                score += BroadcastSuburbTokenBonus;
            }
        }

        score += AudienceScore(candidate, request);
        score += BudgetScore(candidate, request);
        score += MediaPreferenceScore(candidate, request);
        score += ObjectiveFitScore(candidate, request);
        score += StrategyFitScore(candidate, request);
        score += RadioIntelligenceFitScore(candidate, request);
        score += OohIntelligenceFitScore(candidate, request);
        score += AvailabilityScore(candidate);
        score += OohPriorityScore(candidate, request);
        score += DistanceScore(candidate, request);
        score += MixTargetScore(candidate, request);
        score += IndustryContextFitScore(candidate, request);

        if (candidate.MediaType.Equals("Radio", StringComparison.OrdinalIgnoreCase))
        {
            score += RadioFitBonus(candidate, request);
        }

        return score;
    }

    private decimal ScoreArchetypeMediaFit(string archetype, InventoryCandidate candidate)
    {
        var profile = _industryArchetypeScoringService.Resolve(archetype);
        if (profile is null)
        {
            return 0m;
        }

        var mediaType = NormalizeStrategyToken(candidate.MediaType);
        return profile.MediaTypeScores.TryGetValue(mediaType, out var score) ? score : 0m;
    }

    private decimal ScoreArchetypeMetadataFit(string archetype, InventoryCandidate candidate)
    {
        var profile = _industryArchetypeScoringService.Resolve(archetype);
        if (profile is null)
        {
            return 0m;
        }

        if (MatchesMetadataToken(candidate, archetype, "industryFitTags", "industry_fit_tags", "industryArchetypes", "industry_archetypes"))
        {
            return profile.MetadataTagMatchScore;
        }

        return ScoreAudienceHintMatch(candidate, profile);
    }

    private static decimal ScoreAudienceHintMatch(InventoryCandidate candidate, IndustryArchetypeScoringProfile profile)
    {
        var text = BuildAudienceSearchText(candidate);
        if (string.IsNullOrWhiteSpace(text) || profile.AudienceHintScores.Count == 0)
        {
            return 0m;
        }

        decimal bestScore = 0m;
        foreach (var hintScore in profile.AudienceHintScores)
        {
            if (text.Contains(hintScore.Key, StringComparison.OrdinalIgnoreCase))
            {
                bestScore = Math.Max(bestScore, hintScore.Value);
            }
        }

        return bestScore;
    }

    private static decimal ScoreAudienceRequestFit(InventoryCandidate candidate, CampaignPlanningRequest request)
    {
        var audienceNotes = request.TargetAudienceNotes;
        if (string.IsNullOrWhiteSpace(audienceNotes))
        {
            return 0m;
        }

        var candidateText = BuildAudienceSearchText(candidate);
        if (string.IsNullOrWhiteSpace(candidateText))
        {
            return 0m;
        }

        var matched = TokenizeAudienceTerms(audienceNotes)
            .Take(10)
            .Count(token => candidateText.Contains(token, StringComparison.OrdinalIgnoreCase));

        return matched switch
        {
            >= 3 => 4m,
            2 => 3m,
            1 => 1m,
            _ => 0m
        };
    }

    private static decimal ScoreGenderRequestFit(InventoryCandidate candidate, CampaignPlanningRequest request)
    {
        var targetGender = NormalizeGender(request.TargetGender);
        if (string.IsNullOrWhiteSpace(targetGender) || targetGender == "all")
        {
            return 0m;
        }

        var genderText = GetMetadataText(candidate, "audienceGenderSkew", "audience_gender_skew", "targetAudience", "target_audience");
        if (string.IsNullOrWhiteSpace(genderText))
        {
            return 0m;
        }

        var hasTargetMatch = GenderAliases(targetGender)
            .Any(alias => genderText.Contains(alias, StringComparison.OrdinalIgnoreCase));
        if (hasTargetMatch)
        {
            return 3m;
        }

        var oppositeGender = targetGender == "male" ? "female" : "male";
        var hasOppositeMatch = GenderAliases(oppositeGender)
            .Any(alias => genderText.Contains(alias, StringComparison.OrdinalIgnoreCase));
        return hasOppositeMatch ? -2m : 0m;
    }

    private HashSet<string> ResolveIndustryArchetypes(CampaignPlanningRequest request)
    {
        var archetypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var hints = request.TargetInterests
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Concat(new[] { request.TargetAudienceNotes })
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .SelectMany(value => new[] { value!.Trim() }.Concat(TokenizeAudienceTerms(value)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        foreach (var hint in hints)
        {
            var industryCode = _leadMasterDataService.ResolveIndustryFromHints(new[] { hint })?.Code;
            if (_industryArchetypeScoringService.Resolve(industryCode) is not null)
            {
                archetypes.Add(industryCode!);
                continue;
            }

            var normalizedHint = NormalizeStrategyToken(hint);
            if (ContainsAnyStrategyToken(normalizedHint, "automotive", "dealership", "vehicle", "car", "motor"))
            {
                archetypes.Add(LeadCanonicalValues.IndustryCodes.Automotive);
            }
            else if (ContainsAnyStrategyToken(normalizedHint, "restaurant", "food", "takeaway", "cafe", "diner", "pizza"))
            {
                archetypes.Add(LeadCanonicalValues.IndustryCodes.FoodHospitality);
            }
        }

        return archetypes;
    }

    private static bool ContainsAnyStrategyToken(string normalizedSource, params string[] aliases)
    {
        return aliases.Any(alias => normalizedSource.Contains(NormalizeStrategyToken(alias), StringComparison.OrdinalIgnoreCase));
    }

    private decimal RadioFitBonus(InventoryCandidate candidate, CampaignPlanningRequest request)
    {
        decimal bonus = 0m;

        if (!string.IsNullOrWhiteSpace(candidate.TimeBand))
        {
            bonus += Matches(candidate.TimeBand, "breakfast") || Matches(candidate.TimeBand, "drive") ? 8m : 4m;
        }

        if (!string.IsNullOrWhiteSpace(candidate.DayType) && Matches(candidate.DayType, "weekday"))
        {
            bonus += 3m;
        }

        if (!string.IsNullOrWhiteSpace(candidate.SlotType) && Matches(candidate.SlotType, "commercial"))
        {
            bonus += 4m;
        }

        bonus += ReachPriorityBonus(candidate);
        bonus += LocalProvincialRelevanceBonus(candidate, request);
        bonus += _policyService.GetHigherBandRadioBonus(candidate, request);

        return bonus;
    }

    private decimal ReachPriorityBonus(InventoryCandidate candidate)
    {
        if (!candidate.MonthlyListenership.HasValue || candidate.MonthlyListenership.Value <= 0)
        {
            return 0m;
        }

        var monthlyReach = candidate.MonthlyListenership.Value;
        if (monthlyReach >= 5_000_000) return 12m;
        if (monthlyReach >= 3_000_000) return 9m;
        if (monthlyReach >= 1_500_000) return 6m;
        if (monthlyReach >= 750_000) return 3m;
        return 0m;
    }

    private decimal LocalProvincialRelevanceBonus(InventoryCandidate candidate, CampaignPlanningRequest request)
    {
        var scope = NormalizeScope(request.GeographyScope);
        if (scope is not ("local" or "provincial"))
        {
            return 0m;
        }

        decimal adjustment = 0m;
        if (IsMainstreamHighReachStation(candidate.DisplayName))
        {
            adjustment += 8m;
        }

        // Prevent broad pan-Africa stations from dominating strictly local/provincial plans.
        if (LooksPanRegionalGlobalAudience(candidate))
        {
            adjustment -= 8m;
        }

        return adjustment;
    }

    private static bool IsMainstreamHighReachStation(string? displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            return false;
        }

        return displayName.Contains("metro fm", StringComparison.OrdinalIgnoreCase)
            || displayName.Contains("5fm", StringComparison.OrdinalIgnoreCase)
            || displayName.Contains("5 fm", StringComparison.OrdinalIgnoreCase);
    }

    private static bool LooksPanRegionalGlobalAudience(InventoryCandidate candidate)
    {
        if (candidate.DisplayName.Contains("channel africa", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var audienceText = GetMetadataText(candidate,
            "targetAudience",
            "target_audience",
            "inventoryIntelligenceNotes",
            "inventory_intelligence_notes");
        if (string.IsNullOrWhiteSpace(audienceText))
        {
            return false;
        }

        return audienceText.Contains("pan-afric", StringComparison.OrdinalIgnoreCase)
            || audienceText.Contains("pan african", StringComparison.OrdinalIgnoreCase)
            || audienceText.Contains("across africa", StringComparison.OrdinalIgnoreCase)
            || audienceText.Contains("international", StringComparison.OrdinalIgnoreCase);
    }

    private static decimal StrategyFitScore(InventoryCandidate candidate, CampaignPlanningRequest request)
    {
        var signals = CampaignStrategySupport.BuildSignals(request);
        var mediaType = candidate.MediaType.Trim().ToLowerInvariant();
        decimal score = 0m;

        if (signals.PremiumAudience)
        {
            if (mediaType is "ooh" or "tv")
            {
                score += 4m;
            }

            if (HasLsmOverlap(candidate, 7, 10))
            {
                score += 6m;
            }
        }

        if (signals.MassMarketAudience)
        {
            if (mediaType is "radio" or "ooh")
            {
                score += 4m;
            }

            if (HasLsmOverlap(candidate, 4, 7))
            {
                score += 5m;
            }
        }

        if (signals.FastDecisionCycle || signals.ImmediateUrgency)
        {
            score += mediaType switch
            {
                "radio" => 6m,
                "ooh" => 5m,
                "digital" => 5m,
                "tv" => 1m,
                _ => 0m
            };
        }
        else if (signals.LongDecisionCycle)
        {
            score += mediaType switch
            {
                "tv" => 5m,
                "ooh" => 4m,
                "radio" => 3m,
                "digital" => 3m,
                _ => 0m
            };
        }

        if (signals.WalkInDriven)
        {
            score += mediaType switch
            {
                "ooh" => 6m,
                "radio" => 4m,
                _ => 0m
            };
        }

        if (signals.OnlineDriven)
        {
            score += mediaType switch
            {
                "digital" => 8m,
                "radio" => 2m,
                "ooh" => -1m,
                _ => 0m
            };
        }

        if (signals.AudienceClearlyDefined && HasAudienceMetadata(candidate))
        {
            score += 3m;
        }

        if (signals.AudienceNeedsBroadReach)
        {
            if (mediaType is "ooh" or "tv")
            {
                score += 4m;
            }

            if (ResolveCandidateCoverage(candidate) is "national" or "provincial")
            {
                score += 2m;
            }
        }

        if (signals.HighGrowthAmbition && mediaType is "ooh" or "radio" or "tv")
        {
            score += 2m;
        }

        if (signals.EnterpriseOrGovernment && mediaType == "radio")
        {
            score += 3m;
        }

        if (MatchesStrategyMetadata(candidate, request.BuyingBehaviour, "buyingBehaviourFit", "buying_behaviour_fit"))
        {
            score += 5m;
        }

        if (MatchesStrategyMetadata(candidate, request.PricePositioning, "pricePositioningFit", "price_positioning_fit"))
        {
            score += 5m;
        }

        if (MatchesStrategyMetadata(candidate, request.SalesModel, "salesModelFit", "sales_model_fit"))
        {
            score += 6m;
        }

        if (signals.PremiumAudience && MatchesMetadataToken(candidate, "premium", "premiumMassFit", "premium_mass_fit"))
        {
            score += 5m;
        }

        if (signals.MassMarketAudience && MatchesMetadataToken(candidate, "mass_market", "premiumMassFit", "premium_mass_fit"))
        {
            score += 5m;
        }

        return Math.Min(22m, Math.Max(-4m, score));
    }

    private static decimal AgeScore(InventoryCandidate candidate, CampaignPlanningRequest request)
    {
        if (!request.TargetAgeMin.HasValue && !request.TargetAgeMax.HasValue)
        {
            return 0m;
        }

        var ageText = GetMetadataText(candidate, "audienceAgeSkew", "audience_age_skew", "targetAudience", "target_audience");
        if (string.IsNullOrWhiteSpace(ageText))
        {
            return 0m;
        }

        var requestedMin = request.TargetAgeMin ?? request.TargetAgeMax ?? 13;
        var requestedMax = request.TargetAgeMax ?? request.TargetAgeMin ?? 100;
        if (TryParseAgeRange(ageText, out var candidateMin, out var candidateMax))
        {
            var overlap = !(candidateMax < requestedMin || candidateMin > requestedMax);
            return overlap ? 8m : 0m;
        }

        var requestedTokens = BuildAgeTokens(requestedMin, requestedMax);
        return requestedTokens.Any(token => ageText.Contains(token, StringComparison.OrdinalIgnoreCase))
            ? 5m
            : 0m;
    }

    private static decimal GenderScore(InventoryCandidate candidate, CampaignPlanningRequest request)
    {
        var targetGender = NormalizeGender(request.TargetGender);
        if (string.IsNullOrWhiteSpace(targetGender) || targetGender == "all")
        {
            return 0m;
        }

        var genderText = GetMetadataText(candidate, "audienceGenderSkew", "audience_gender_skew", "targetAudience", "target_audience");
        if (string.IsNullOrWhiteSpace(genderText))
        {
            return 0m;
        }

        return GenderAliases(targetGender).Any(alias => genderText.Contains(alias, StringComparison.OrdinalIgnoreCase))
            ? 5m
            : 0m;
    }

    private static decimal AudienceKeywordScore(InventoryCandidate candidate, CampaignPlanningRequest request)
    {
        var candidateText = BuildAudienceSearchText(candidate);
        if (string.IsNullOrWhiteSpace(candidateText))
        {
            return 0m;
        }

        var matches = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var interest in request.TargetInterests)
        {
            foreach (var token in TokenizeAudienceTerms(interest))
            {
                if (candidateText.Contains(token, StringComparison.OrdinalIgnoreCase))
                {
                    matches.Add(token);
                }
            }
        }

        foreach (var token in TokenizeAudienceTerms(request.TargetAudienceNotes).Take(8))
        {
            if (candidateText.Contains(token, StringComparison.OrdinalIgnoreCase))
            {
                matches.Add(token);
            }
        }

        return matches.Count switch
        {
            >= 3 => 8m,
            2 => 6m,
            1 => 4m,
            _ => 0m
        };
    }

    private static decimal ObjectiveFitScore(InventoryCandidate candidate, CampaignPlanningRequest request)
    {
        var objective = (request.Objective ?? string.Empty).Trim().ToLowerInvariant();
        if (objective.Length == 0)
        {
            return 0m;
        }

        var mediaType = candidate.MediaType.Trim().ToLowerInvariant();
        var score = objective switch
        {
            "awareness" or "brand_presence" => mediaType switch
            {
                "ooh" => 10m,
                "tv" => 9m,
                "radio" => 7m,
                "digital" => 6m,
                _ => 0m
            },
            "launch" => mediaType switch
            {
                "ooh" => 10m,
                "radio" => 8m,
                "tv" => 8m,
                "digital" => 6m,
                _ => 0m
            },
            "promotion" => mediaType switch
            {
                "radio" => 9m,
                "ooh" => 8m,
                "digital" => 7m,
                "tv" => 4m,
                _ => 0m
            },
            "leads" => mediaType switch
            {
                "digital" => 10m,
                "radio" => 8m,
                "ooh" => 3m,
                "tv" => 2m,
                _ => 0m
            },
            "foot_traffic" => mediaType switch
            {
                "ooh" => 10m,
                "radio" => 8m,
                "digital" => 5m,
                "tv" => 2m,
                _ => 0m
            },
            _ => 0m
        };

        if (mediaType == "radio" && (!string.IsNullOrWhiteSpace(candidate.TimeBand) || !string.IsNullOrWhiteSpace(candidate.DayType)))
        {
            if (objective is "leads" or "promotion" or "foot_traffic")
            {
                if (Matches(candidate.TimeBand, "breakfast") || Matches(candidate.TimeBand, "drive"))
                {
                    score += 2m;
                }

                if (Matches(candidate.DayType, "weekday"))
                {
                    score += 1m;
                }
            }
        }

        if (mediaType == "ooh" && objective is "awareness" or "brand_presence" or "launch" or "foot_traffic")
        {
            if (!string.IsNullOrWhiteSpace(candidate.Area) || !string.IsNullOrWhiteSpace(candidate.City))
            {
                score += 1m;
            }
        }

        if (MatchesMetadataToken(candidate, objective, "objectiveFitPrimary", "objective_fit_primary"))
        {
            score += 8m;
        }
        else if (MatchesMetadataToken(candidate, objective, "objectiveFitSecondary", "objective_fit_secondary"))
        {
            score += 4m;
        }

        return score;
    }

    private static decimal AvailabilityScore(InventoryCandidate candidate) => candidate.IsAvailable ? 10m : 0m;

    private static decimal OohPriorityScore(InventoryCandidate candidate, CampaignPlanningRequest request)
    {
        if (!candidate.MediaType.Equals("OOH", StringComparison.OrdinalIgnoreCase))
        {
            return 0m;
        }

        var preferredOoh = request.PreferredMediaTypes.Any(preferred =>
            Matches(preferred, "ooh") || Matches(preferred, candidate.MediaType) || Matches(preferred, candidate.Subtype));

        return preferredOoh ? 30m : 18m;
    }

    private static decimal OohIntelligenceFitScore(InventoryCandidate candidate, CampaignPlanningRequest request)
    {
        if (!candidate.MediaType.Equals("OOH", StringComparison.OrdinalIgnoreCase))
        {
            return 0m;
        }

        decimal score = 0m;
        var strategySignals = CampaignStrategySupport.BuildSignals(request);
        var audienceIntent = string.Join(" ",
            request.TargetInterests.Where(static value => !string.IsNullOrWhiteSpace(value)),
            request.TargetAudienceNotes,
            request.CustomerType,
            request.ValuePropositionFocus,
            request.Objective);

        if (strategySignals.PremiumAudience)
        {
            score += ScoreFitBand(candidate, "highValueShopperFit", "high_value_shopper_fit", high: 6m, medium: 3m);
            score += ScoreMetadataMatch(candidate, "premium_mall", "venueType", "venue_type", exact: 4m);
        }

        if (strategySignals.MassMarketAudience)
        {
            score += ScoreMetadataMatch(candidate, "mass_market", "premiumMassFit", "premium_mass_fit", exact: 4m);
            score += ScoreMetadataMatch(candidate, "community_mall", "venueType", "venue_type", exact: 3m);
        }

        if (MatchesAudienceIntent(audienceIntent, "youth", "student", "campus", "young"))
        {
            score += ScoreFitBand(candidate, "youthFit", "youth_fit", high: 6m, medium: 3m);
        }

        if (MatchesAudienceIntent(audienceIntent, "family", "families", "parents", "kids", "children"))
        {
            score += ScoreFitBand(candidate, "familyFit", "family_fit", high: 5m, medium: 3m);
        }

        if (MatchesAudienceIntent(audienceIntent, "professional", "executive", "b2b", "office", "corporate"))
        {
            score += ScoreFitBand(candidate, "professionalFit", "professional_fit", high: 5m, medium: 3m);
        }

        if (MatchesAudienceIntent(audienceIntent, "commuter", "transport", "traffic", "taxi", "rank"))
        {
            score += ScoreFitBand(candidate, "commuterFit", "commuter_fit", high: 5m, medium: 3m);
        }

        if (MatchesAudienceIntent(audienceIntent, "tourist", "tourism", "visitor", "visitors", "leisure"))
        {
            score += ScoreFitBand(candidate, "touristFit", "tourist_fit", high: 4m, medium: 2m);
        }

        if ((request.Objective ?? string.Empty).Trim().Equals("foot_traffic", StringComparison.OrdinalIgnoreCase))
        {
            score += ScoreFitBand(candidate, "dwellTimeScore", "dwell_time_score", high: 5m, medium: 2m);
            score += ScoreMetadataMatch(candidate, "mall_interior", "environmentType", "environment_type", exact: 3m);
            score += ScoreMetadataMatch(candidate, "food_court", "environmentType", "environment_type", exact: 3m);
        }

        if ((request.Objective ?? string.Empty).Trim().Equals("awareness", StringComparison.OrdinalIgnoreCase)
            || (request.Objective ?? string.Empty).Trim().Equals("brand_presence", StringComparison.OrdinalIgnoreCase)
            || (request.Objective ?? string.Empty).Trim().Equals("launch", StringComparison.OrdinalIgnoreCase))
        {
            score += ScoreMetadataMatch(candidate, "premium_mall", "venueType", "venue_type", exact: 2m);
            score += ScoreMetadataMatch(candidate, "lifestyle_centre", "venueType", "venue_type", exact: 2m);
            score += ScoreFitBand(candidate, "dwellTimeScore", "dwell_time_score", high: 3m, medium: 1m);
        }

        return Math.Min(16m, Math.Max(0m, score));
    }

    private static decimal RadioIntelligenceFitScore(InventoryCandidate candidate, CampaignPlanningRequest request)
    {
        if (!candidate.MediaType.Equals("Radio", StringComparison.OrdinalIgnoreCase))
        {
            return 0m;
        }

        decimal score = 0m;
        var objective = NormalizeStrategyToken(request.Objective ?? string.Empty);
        var strategySignals = CampaignStrategySupport.BuildSignals(request);
        var audienceIntent = string.Join(" ",
            request.TargetInterests.Where(static value => !string.IsNullOrWhiteSpace(value)),
            request.TargetAudienceNotes,
            request.CustomerType,
            request.ValuePropositionFocus,
            request.Objective);

        if (!string.IsNullOrWhiteSpace(objective))
        {
            score += ScoreMetadataMatch(candidate, objective, "objectiveFitPrimary", "objective_fit_primary", exact: 8m);
            score += ScoreMetadataMatch(candidate, objective, "objectiveFitSecondary", "objective_fit_secondary", exact: 4m);
        }

        if (MatchesAudienceIntent(audienceIntent, "youth", "student", "young", "gen z"))
        {
            score += ScoreFitBand(candidate, "youthFit", "youth_fit", high: 5m, medium: 3m);
        }

        if (MatchesAudienceIntent(audienceIntent, "family", "families", "parents", "children"))
        {
            score += ScoreFitBand(candidate, "familyFit", "family_fit", high: 5m, medium: 3m);
        }

        if (MatchesAudienceIntent(audienceIntent, "professional", "executive", "office", "corporate", "b2b"))
        {
            score += ScoreFitBand(candidate, "professionalFit", "professional_fit", high: 5m, medium: 3m);
            score += ScoreFitBand(candidate, "businessDecisionMakerFit", "business_decision_maker_fit", high: 5m, medium: 3m);
        }

        if (MatchesAudienceIntent(audienceIntent, "commuter", "taxi", "traffic", "drive"))
        {
            score += ScoreFitBand(candidate, "commuterFit", "commuter_fit", high: 5m, medium: 3m);
        }

        if (strategySignals.PremiumAudience)
        {
            score += ScoreFitBand(candidate, "highValueClientFit", "high_value_client_fit", high: 5m, medium: 3m);
            score += ScoreMetadataMatch(candidate, "premium", "premiumMassFit", "premium_mass_fit", exact: 3m);
        }

        if (strategySignals.MassMarketAudience)
        {
            score += ScoreMetadataMatch(candidate, "mass_market", "premiumMassFit", "premium_mass_fit", exact: 4m);
            score += ScoreMetadataMatch(candidate, "mid_market", "premiumMassFit", "premium_mass_fit", exact: 2m);
        }

        if (strategySignals.FastDecisionCycle || strategySignals.ImmediateUrgency || strategySignals.WalkInDriven)
        {
            score += ScoreFitBand(candidate, "commuterFit", "commuter_fit", high: 4m, medium: 2m);

            var behaviourText = GetMetadataText(candidate, "buyingBehaviourFit", "buying_behaviour_fit");
            if (MatchesAudienceIntent(behaviourText, "impulse", "convenience", "passive"))
            {
                score += 3m;
            }
        }

        if (objective is "awareness" or "launch" or "brand_presence")
        {
            score += ScoreFitBand(candidate, "morningDriveFit", "morning_drive_fit", high: 4m, medium: 2m);
            score += ScoreFitBand(candidate, "afternoonDriveFit", "afternoon_drive_fit", high: 4m, medium: 2m);
        }

        if (objective is "leads" or "consideration")
        {
            score += ScoreFitBand(candidate, "workdayFit", "workday_fit", high: 3m, medium: 2m);
            score += ScoreFitBand(candidate, "businessDecisionMakerFit", "business_decision_maker_fit", high: 3m, medium: 2m);
        }

        return Math.Min(22m, Math.Max(0m, score));
    }

    private static decimal DistanceScore(InventoryCandidate candidate, CampaignPlanningRequest request)
    {
        if (!candidate.MediaType.Equals("OOH", StringComparison.OrdinalIgnoreCase))
        {
            return 0m;
        }

        if (!candidate.Latitude.HasValue || !candidate.Longitude.HasValue)
        {
            return 0m;
        }

        if (!request.TargetLatitude.HasValue || !request.TargetLongitude.HasValue)
        {
            return 0m;
        }

        var distanceKm = HaversineDistanceKm(
            request.TargetLatitude.Value,
            request.TargetLongitude.Value,
            candidate.Latitude.Value,
            candidate.Longitude.Value);

        return distanceKm switch
        {
            <= 5d => 14m,
            <= 15d => 10m,
            <= 30d => 7m,
            <= 50d => 4m,
            <= 80d => 2m,
            _ => 0m
        };
    }

    private static double HaversineDistanceKm(double startLatitude, double startLongitude, double endLatitude, double endLongitude)
    {
        const double earthRadiusKm = 6371d;
        var latitudeDeltaRadians = ToRadians(endLatitude - startLatitude);
        var longitudeDeltaRadians = ToRadians(endLongitude - startLongitude);

        var a = Math.Sin(latitudeDeltaRadians / 2d) * Math.Sin(latitudeDeltaRadians / 2d)
            + Math.Cos(ToRadians(startLatitude)) * Math.Cos(ToRadians(endLatitude))
            * Math.Sin(longitudeDeltaRadians / 2d) * Math.Sin(longitudeDeltaRadians / 2d);
        var c = 2d * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1d - a));
        return earthRadiusKm * c;
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180d;

    private static bool HasLsmOverlap(InventoryCandidate candidate, int requestMin, int requestMax)
    {
        if (candidate.LsmMin.HasValue && candidate.LsmMax.HasValue)
        {
            return !(candidate.LsmMax.Value < requestMin || candidate.LsmMin.Value > requestMax);
        }

        var lsmText = GetMetadataText(candidate, "audienceLsmRange", "audience_lsm_range", "targetAudience", "target_audience");
        if (string.IsNullOrWhiteSpace(lsmText))
        {
            return false;
        }

        var values = Regex.Matches(lsmText, "\\d+")
            .Select(match => int.TryParse(match.Value, out var parsed) ? parsed : (int?)null)
            .Where(value => value.HasValue)
            .Select(value => value!.Value)
            .ToArray();

        if (values.Length >= 2)
        {
            var candidateMin = Math.Min(values[0], values[1]);
            var candidateMax = Math.Max(values[0], values[1]);
            return !(candidateMax < requestMin || candidateMin > requestMax);
        }

        return false;
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
            "salesModelFit",
            "sales_model_fit",
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
            "commuter_fit",
            "touristFit",
            "tourist_fit",
            "dwellTimeScore",
            "dwell_time_score",
            "primaryAudienceTags",
            "primary_audience_tags",
            "secondaryAudienceTags",
            "secondary_audience_tags",
            "recommendationTags",
            "recommendation_tags"));
    }

    private static decimal GetComparableMonthlyCost(InventoryCandidate candidate)
    {
        if (candidate.Metadata.TryGetValue("monthlyCostEstimateZar", out var monthlyCost)
            && TryGetDecimal(monthlyCost, out var parsedMonthlyCost)
            && parsedMonthlyCost > 0m)
        {
            return parsedMonthlyCost;
        }

        if (candidate.Metadata.TryGetValue("monthly_cost_estimate_zar", out var snakeCaseMonthlyCost)
            && TryGetDecimal(snakeCaseMonthlyCost, out parsedMonthlyCost)
            && parsedMonthlyCost > 0m)
        {
            return parsedMonthlyCost;
        }

        return candidate.Cost;
    }

    private static bool TryGetDecimal(object? value, out decimal parsed)
    {
        switch (value)
        {
            case decimal decimalValue:
                parsed = decimalValue;
                return true;
            case double doubleValue:
                parsed = (decimal)doubleValue;
                return true;
            case float floatValue:
                parsed = (decimal)floatValue;
                return true;
            case int intValue:
                parsed = intValue;
                return true;
            case long longValue:
                parsed = longValue;
                return true;
            case string text when decimal.TryParse(text, out parsed):
                return true;
            case System.Text.Json.JsonElement element when element.ValueKind == System.Text.Json.JsonValueKind.Number && element.TryGetDecimal(out parsed):
                return true;
            case System.Text.Json.JsonElement element when element.ValueKind == System.Text.Json.JsonValueKind.String && decimal.TryParse(element.GetString(), out parsed):
                return true;
            default:
                parsed = 0m;
                return false;
        }
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

    private static string ResolveCandidateCoverage(InventoryCandidate candidate)
    {
        var scope = (candidate.MarketScope ?? string.Empty).Trim().ToLowerInvariant();
        if (scope.Contains("national", StringComparison.OrdinalIgnoreCase))
        {
            return "national";
        }

        if (scope.Contains("provincial", StringComparison.OrdinalIgnoreCase)
            || scope.Contains("regional", StringComparison.OrdinalIgnoreCase)
            || scope.Contains("province", StringComparison.OrdinalIgnoreCase))
        {
            return "provincial";
        }

        if (scope.Contains("local", StringComparison.OrdinalIgnoreCase)
            || scope.Contains("city", StringComparison.OrdinalIgnoreCase)
            || scope.Contains("suburb", StringComparison.OrdinalIgnoreCase))
        {
            return "local";
        }

        if (!string.IsNullOrWhiteSpace(candidate.City) || !string.IsNullOrWhiteSpace(candidate.Suburb))
        {
            return "local";
        }

        if (!string.IsNullOrWhiteSpace(candidate.Province) || !string.IsNullOrWhiteSpace(candidate.RegionClusterCode))
        {
            return "provincial";
        }

        return "unknown";
    }

    private bool MatchesGeo(string? left, string? right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right)) return false;
        if (string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return _broadcastMasterDataService.NormalizeGeographyForMatching(left)
            == _broadcastMasterDataService.NormalizeGeographyForMatching(right);
    }

    private static bool Matches(string? left, string? right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
        {
            return false;
        }

        return string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private bool MatchesAnyMetadataToken(InventoryCandidate candidate, string requestedValue, params string[] keys)
    {
        if (string.IsNullOrWhiteSpace(requestedValue) || candidate.Metadata.Count == 0)
        {
            return false;
        }

        return keys.Any(key =>
            candidate.Metadata.TryGetValue(key, out var value)
            && ExtractMetadataTokens(value).Any(token => MatchesGeo(requestedValue, token) || MatchesLanguage(requestedValue, token)));
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

    private static string GetMetadataText(InventoryCandidate candidate, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (candidate.Metadata.TryGetValue(key, out var value))
            {
                var flattened = string.Join(" ", ExtractMetadataTokens(value));
                if (!string.IsNullOrWhiteSpace(flattened))
                {
                    return flattened;
                }
            }
        }

        return string.Empty;
    }

    private static string BuildAudienceSearchText(InventoryCandidate candidate)
    {
        var parts = new[]
            {
                candidate.DisplayName,
                candidate.MediaType,
                candidate.Subtype,
                candidate.Language,
                GetMetadataText(candidate, "targetAudience", "target_audience", "notes", "packageName", "package_name", "audienceAgeSkew", "audience_age_skew", "audienceGenderSkew", "audience_gender_skew", "environmentType", "environment_type", "inventoryIntelligenceNotes", "inventory_intelligence_notes", "venueType", "venue_type", "premiumMassFit", "premium_mass_fit", "pricePositioningFit", "price_positioning_fit", "youthFit", "youth_fit", "familyFit", "family_fit", "professionalFit", "professional_fit", "commuterFit", "commuter_fit", "touristFit", "tourist_fit", "highValueShopperFit", "high_value_shopper_fit", "dwellTimeScore", "dwell_time_score")
            }
            .Where(static part => !string.IsNullOrWhiteSpace(part))
            .ToList()!;

        foreach (var key in new[] { "audienceKeywords", "audience_keywords", "keywords", "primaryAudienceTags", "primary_audience_tags", "secondaryAudienceTags", "secondary_audience_tags", "recommendationTags", "recommendation_tags" })
        {
            if (candidate.Metadata.TryGetValue(key, out var value))
            {
                parts.AddRange(ExtractMetadataTokens(value));
            }
        }

        return string.Join(" ", parts.Where(static part => !string.IsNullOrWhiteSpace(part)));
    }

    private static bool MatchesStrategyMetadata(InventoryCandidate candidate, string? requestedValue, params string[] keys)
    {
        if (string.IsNullOrWhiteSpace(requestedValue))
        {
            return false;
        }

        return MatchesMetadataToken(candidate, requestedValue, keys);
    }

    private static bool MatchesMetadataToken(InventoryCandidate candidate, string requestedValue, params string[] keys)
    {
        return keys.Any(key =>
            candidate.Metadata.TryGetValue(key, out var value)
            && ExtractMetadataTokens(value).Any(token => MatchesStrategyToken(requestedValue, token)));
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

    private static bool MatchesAudienceIntent(string text, params string[] tokens)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        return tokens.Any(token => text.Contains(token, StringComparison.OrdinalIgnoreCase));
    }

    private static decimal ScoreFitBand(InventoryCandidate candidate, string camelKey, string snakeKey, decimal high, decimal medium)
    {
        var raw = GetMetadataText(candidate, camelKey, snakeKey).Trim().ToLowerInvariant();
        return raw switch
        {
            "high" => high,
            "medium" => medium,
            _ => 0m
        };
    }

    private static decimal ScoreMetadataMatch(InventoryCandidate candidate, string expectedToken, string camelKey, string snakeKey, decimal exact)
    {
        return MatchesMetadataToken(candidate, expectedToken, camelKey, snakeKey) ? exact : 0m;
    }

    private static IEnumerable<string> TokenizeAudienceTerms(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            yield break;
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in Regex.Matches(text, "[A-Za-z]{4,}"))
        {
            var token = match.Value.Trim().ToLowerInvariant();
            if (token is "that" or "this" or "with" or "from" or "your" or "their" or "have" or "into" or "across" or "need" or "needs" or "market" or "audience" or "customer" or "customers")
            {
                continue;
            }

            if (seen.Add(token))
            {
                yield return token;
            }
        }
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

        return normalized switch
        {
            var value when value.Contains("youth", StringComparison.OrdinalIgnoreCase) => AssignAgeRange(15, 24, out min, out max),
            var value when value.Contains("young", StringComparison.OrdinalIgnoreCase) => AssignAgeRange(18, 34, out min, out max),
            var value when value.Contains("adult", StringComparison.OrdinalIgnoreCase) => AssignAgeRange(25, 54, out min, out max),
            var value when value.Contains("family", StringComparison.OrdinalIgnoreCase) => AssignAgeRange(25, 54, out min, out max),
            _ => false
        };
    }

    private static bool AssignAgeRange(int rangeMin, int rangeMax, out int min, out int max)
    {
        min = rangeMin;
        max = rangeMax;
        return true;
    }

    private static IEnumerable<string> BuildAgeTokens(int min, int max)
    {
        if (max <= 24)
        {
            yield return "youth";
        }

        if (min <= 34 && max >= 18)
        {
            yield return "young";
        }

        if (max >= 25)
        {
            yield return "adult";
        }
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

    private bool MatchesLanguage(string? left, string? right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
        {
            return false;
        }

        return string.Equals(
            _broadcastMasterDataService.NormalizeLanguageForMatching(left),
            _broadcastMasterDataService.NormalizeLanguageForMatching(right),
            StringComparison.OrdinalIgnoreCase);
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

    private sealed class NoOpLeadMasterDataService : ILeadMasterDataService
    {
        public LeadMasterTokenSet GetTokenSet() => new();
        public MasterLocationMatch? ResolveLocation(string? value) => null;
        public MasterIndustryMatch? ResolveIndustry(string? value) => null;
        public MasterIndustryMatch? ResolveIndustryFromHints(IReadOnlyList<string> hints) => null;
        public MasterLanguageMatch? ResolveLanguage(string? value) => null;
    }

    private sealed class NoOpIndustryArchetypeScoringService : IIndustryArchetypeScoringService
    {
        public IndustryArchetypeScoringProfile? Resolve(string? industryCode) => null;

        public IReadOnlyCollection<string> GetSupportedIndustryCodes() => Array.Empty<string>();
    }
}
