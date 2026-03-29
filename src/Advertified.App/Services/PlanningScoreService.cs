using Advertified.App.Contracts.Campaigns;
using Advertified.App.Domain.Campaigns;
using Advertified.App.Services.Abstractions;

namespace Advertified.App.Services;

public sealed class PlanningScoreService : IPlanningScoreService
{
    private readonly IPlanningPolicyService _policyService;

    public PlanningScoreService(IPlanningPolicyService policyService)
    {
        _policyService = policyService;
    }

    public PlanningCandidateAnalysis AnalyzeCandidate(InventoryCandidate candidate, CampaignPlanningRequest request)
    {
        return new PlanningCandidateAnalysis(ScoreCandidate(candidate, request), Array.Empty<string>(), Array.Empty<string>(), 0m);
    }

    public decimal GeoScore(InventoryCandidate candidate, CampaignPlanningRequest request)
    {
        if (request.Suburbs.Any(x => Matches(x, candidate.Suburb) || Matches(x, candidate.Area))) return 30m;
        if (request.Cities.Any(x => Matches(x, candidate.City))) return 24m;
        if (request.Provinces.Any(x => Matches(x, candidate.Province))) return 16m;
        if (request.Areas.Any(x => Matches(x, candidate.Area) || Matches(x, candidate.Suburb))) return 22m;
        return 4m;
    }

    public decimal AudienceScore(InventoryCandidate candidate, CampaignPlanningRequest request)
    {
        decimal score = 0m;

        if (request.TargetLanguages.Count > 0 && !string.IsNullOrWhiteSpace(candidate.Language))
        {
            if (request.TargetLanguages.Any(x => Matches(x, candidate.Language)))
            {
                score += 10m;
            }
        }

        if (request.TargetLsmMin.HasValue && request.TargetLsmMax.HasValue && candidate.LsmMin.HasValue && candidate.LsmMax.HasValue)
        {
            var overlap = !(candidate.LsmMax.Value < request.TargetLsmMin.Value || candidate.LsmMin.Value > request.TargetLsmMax.Value);
            if (overlap)
            {
                score += 15m;
            }
        }

        return score;
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

    private decimal ScoreCandidate(InventoryCandidate candidate, CampaignPlanningRequest request)
    {
        decimal score = 0m;

        score += GeoScore(candidate, request);
        score += AudienceScore(candidate, request);
        score += BudgetScore(candidate, request);
        score += MediaPreferenceScore(candidate, request);
        score += AvailabilityScore(candidate);
        score += OohPriorityScore(candidate, request);
        score += MixTargetScore(candidate, request);

        if (candidate.MediaType.Equals("Radio", StringComparison.OrdinalIgnoreCase))
        {
            score += RadioFitBonus(candidate, request);
        }

        return score;
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

        bonus += _policyService.GetHigherBandRadioBonus(candidate, request);

        return bonus;
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

    private static bool Matches(string? left, string? right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right)) return false;
        return string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}
