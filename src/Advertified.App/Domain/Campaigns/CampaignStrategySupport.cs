using Advertified.App.Contracts.Campaigns;

namespace Advertified.App.Domain.Campaigns;

public sealed record CampaignStrategySignals(
    bool PremiumAudience,
    bool MassMarketAudience,
    bool FastDecisionCycle,
    bool LongDecisionCycle,
    bool ImmediateUrgency,
    bool WalkInDriven,
    bool OnlineDriven,
    bool HighGrowthAmbition,
    bool AudienceClearlyDefined,
    bool AudienceNeedsBroadReach,
    bool EnterpriseOrGovernment);

public static class CampaignStrategySupport
{
    public static CampaignStrategySignals BuildSignals(CampaignPlanningRequest request)
    {
        var pricePositioning = Normalize(request.PricePositioning);
        var averageSpendBand = Normalize(request.AverageCustomerSpendBand);
        var decisionCycle = Normalize(request.DecisionCycle);
        var urgencyLevel = Normalize(request.UrgencyLevel);
        var salesModel = Normalize(request.SalesModel);
        var growthTarget = Normalize(request.GrowthTarget);
        var audienceClarity = Normalize(request.AudienceClarity);
        var customerType = Normalize(request.CustomerType);

        var premiumAudience =
            pricePositioning is "premium" or "luxury"
            || averageSpendBand is "r2000_r10000" or "r10000_plus";
        var massMarketAudience =
            pricePositioning == "budget"
            || averageSpendBand == "under_r500";

        return new CampaignStrategySignals(
            PremiumAudience: premiumAudience,
            MassMarketAudience: massMarketAudience,
            FastDecisionCycle: decisionCycle is "same_day" or "1_7_days",
            LongDecisionCycle: decisionCycle == "1_6_months",
            ImmediateUrgency: urgencyLevel is "immediate" or "within_1_month",
            WalkInDriven: salesModel is "walk_ins" or "hybrid",
            OnlineDriven: salesModel == "online_sales",
            HighGrowthAmbition: growthTarget is "3x" or "5x_plus",
            AudienceClearlyDefined: audienceClarity is "very_clear" or "somewhat_clear",
            AudienceNeedsBroadReach: audienceClarity == "unclear",
            EnterpriseOrGovernment: customerType is "corporate" or "government");
    }

    public static (int? Min, int? Max) ResolveSuggestedLsmRange(CampaignPlanningRequest request)
    {
        if (request.TargetLsmMin.HasValue || request.TargetLsmMax.HasValue)
        {
            return (request.TargetLsmMin, request.TargetLsmMax);
        }

        return (Normalize(request.PricePositioning), Normalize(request.AverageCustomerSpendBand)) switch
        {
            ("luxury", _) => (9, 10),
            ("premium", _) => (7, 10),
            ("budget", _) => (4, 7),
            (_, "r10000_plus") => (8, 10),
            (_, "r2000_r10000") => (6, 9),
            (_, "under_r500") => (4, 7),
            _ => (null, null)
        };
    }

    public static IReadOnlyList<string> BuildAudienceTerms(CampaignPlanningRequest request)
    {
        var tokens = new List<string>();
        Add(tokens, request.CustomerType);
        Add(tokens, request.BuyingBehaviour);
        Add(tokens, request.SalesModel);
        Add(tokens, request.PricePositioning);
        Add(tokens, request.ValuePropositionFocus);
        Add(tokens, request.CurrentCustomerNotes);

        return tokens
            .Select(NormalizeDisplayValue)
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static IReadOnlyList<string> BuildContextLines(CampaignPlanningRequest request)
    {
        var lines = new List<string>();
        AddLine(lines, "Business stage", request.BusinessStage);
        AddLine(lines, "Monthly revenue", request.MonthlyRevenueBand);
        AddLine(lines, "Sales model", request.SalesModel);
        AddLine(lines, "Customer type", request.CustomerType);
        AddLine(lines, "Current customers", request.CurrentCustomerNotes);
        AddLine(lines, "Buying behaviour", request.BuyingBehaviour);
        AddLine(lines, "Decision cycle", request.DecisionCycle);
        AddLine(lines, "Price positioning", request.PricePositioning);
        AddLine(lines, "Average spend", request.AverageCustomerSpendBand);
        AddLine(lines, "Growth target", request.GrowthTarget);
        AddLine(lines, "Urgency", request.UrgencyLevel);
        AddLine(lines, "Audience clarity", request.AudienceClarity);
        AddLine(lines, "Value proposition", request.ValuePropositionFocus);
        return lines;
    }

    private static void Add(List<string> tokens, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            tokens.Add(value);
        }
    }

    private static void AddLine(List<string> lines, string label, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            lines.Add($"{label}: {NormalizeDisplayValue(value)}");
        }
    }

    private static string Normalize(string? value)
    {
        return (value ?? string.Empty).Trim().ToLowerInvariant();
    }

    private static string NormalizeDisplayValue(string value)
    {
        var normalized = value
            .Trim()
            .Replace('_', ' ');

        return normalized switch
        {
            "under r50k" => "Under R50k",
            "r50k r200k" => "R50k - R200k",
            "r200k r1m" => "R200k - R1m",
            "over r1m" => "Over R1m",
            "under r500" => "Under R500",
            "r500 r2000" => "R500 - R2,000",
            "r2000 r10000" => "R2,000 - R10,000",
            "r10000 plus" => "R10,000+",
            "within 1 month" => "Within 1 month",
            "within 3 months" => "Within 3 months",
            "same day" => "Same day",
            "1 7 days" => "1 - 7 days",
            "1 4 weeks" => "1 - 4 weeks",
            "1 6 months" => "1 - 6 months+",
            "5x plus" => "5x+",
            _ => normalized
        };
    }
}
