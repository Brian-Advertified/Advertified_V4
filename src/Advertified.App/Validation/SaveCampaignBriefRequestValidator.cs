using Advertified.App.Contracts.Campaigns;
using FluentValidation;

namespace Advertified.App.Validation;

public sealed class SaveCampaignBriefRequestValidator : AbstractValidator<SaveCampaignBriefRequest>
{
    private static readonly HashSet<string> AllowedPlanningObjectives = new(StringComparer.OrdinalIgnoreCase)
    {
        "awareness",
        "leads",
        "foot_traffic",
        "promotion",
        "launch",
        "brand_presence"
    };

    private static readonly HashSet<string> AllowedGeographyScopes = new(StringComparer.OrdinalIgnoreCase)
    {
        "local",
        "provincial",
        "national"
    };

    private static readonly HashSet<string> AllowedVideoAspectRatios = new(StringComparer.OrdinalIgnoreCase)
    {
        "16:9",
        "9:16",
        "1:1",
        "4:5"
    };

    private static readonly HashSet<int> AllowedVideoDurations = new()
    {
        6,
        10,
        15,
        30,
        45,
        60
    };

    private static readonly HashSet<string> AllowedBusinessStages = new(StringComparer.OrdinalIgnoreCase)
    {
        "startup",
        "early_growth",
        "established",
        "mature"
    };

    private static readonly HashSet<string> AllowedMonthlyRevenueBands = new(StringComparer.OrdinalIgnoreCase)
    {
        "under_r50k",
        "r50k_r200k",
        "r200k_r1m",
        "over_r1m"
    };

    private static readonly HashSet<string> AllowedSalesModels = new(StringComparer.OrdinalIgnoreCase)
    {
        "walk_ins",
        "online_sales",
        "direct_sales",
        "referral_based",
        "hybrid"
    };

    private static readonly HashSet<string> AllowedCustomerTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "b2c",
        "smb",
        "corporate",
        "government"
    };

    private static readonly HashSet<string> AllowedBuyingBehaviours = new(StringComparer.OrdinalIgnoreCase)
    {
        "price_sensitive",
        "quality_focused",
        "convenience_driven",
        "brand_conscious",
        "urgency_driven"
    };

    private static readonly HashSet<string> AllowedDecisionCycles = new(StringComparer.OrdinalIgnoreCase)
    {
        "same_day",
        "1_7_days",
        "1_4_weeks",
        "1_6_months"
    };

    private static readonly HashSet<string> AllowedPricePositioning = new(StringComparer.OrdinalIgnoreCase)
    {
        "budget",
        "mid_range",
        "premium",
        "luxury"
    };

    private static readonly HashSet<string> AllowedAverageCustomerSpendBands = new(StringComparer.OrdinalIgnoreCase)
    {
        "under_r500",
        "r500_r2000",
        "r2000_r10000",
        "r10000_plus"
    };

    private static readonly HashSet<string> AllowedGrowthTargets = new(StringComparer.OrdinalIgnoreCase)
    {
        "maintain",
        "2x",
        "3x",
        "5x_plus"
    };

    private static readonly HashSet<string> AllowedUrgencyLevels = new(StringComparer.OrdinalIgnoreCase)
    {
        "immediate",
        "within_1_month",
        "within_3_months"
    };

    private static readonly HashSet<string> AllowedAudienceClarity = new(StringComparer.OrdinalIgnoreCase)
    {
        "very_clear",
        "somewhat_clear",
        "unclear"
    };

    private static readonly HashSet<string> AllowedValuePropositionFocus = new(StringComparer.OrdinalIgnoreCase)
    {
        "lowest_price",
        "highest_quality",
        "speed_convenience",
        "unique_offer",
        "brand_reputation"
    };

    public SaveCampaignBriefRequestValidator()
    {
        RuleFor(x => x.Objective)
            .NotEmpty()
            .Must(v => AllowedPlanningObjectives.Contains(v))
            .WithMessage("Select a valid campaign objective.");

        RuleFor(x => x.GeographyScope)
            .NotEmpty()
            .Must(v => AllowedGeographyScopes.Contains(v))
            .WithMessage("Select a valid geography scope.");

        RuleFor(x => x.BusinessStage)
            .Must(value => string.IsNullOrWhiteSpace(value) || AllowedBusinessStages.Contains(value))
            .WithMessage("Select a valid business stage.");

        RuleFor(x => x.MonthlyRevenueBand)
            .Must(value => string.IsNullOrWhiteSpace(value) || AllowedMonthlyRevenueBands.Contains(value))
            .WithMessage("Select a valid monthly revenue band.");

        RuleFor(x => x.SalesModel)
            .Must(value => string.IsNullOrWhiteSpace(value) || AllowedSalesModels.Contains(value))
            .WithMessage("Select a valid sales model.");

        RuleFor(x => x.CustomerType)
            .Must(value => string.IsNullOrWhiteSpace(value) || AllowedCustomerTypes.Contains(value))
            .WithMessage("Select a valid customer type.");

        RuleFor(x => x.BuyingBehaviour)
            .Must(value => string.IsNullOrWhiteSpace(value) || AllowedBuyingBehaviours.Contains(value))
            .WithMessage("Select a valid buying behaviour.");

        RuleFor(x => x.DecisionCycle)
            .Must(value => string.IsNullOrWhiteSpace(value) || AllowedDecisionCycles.Contains(value))
            .WithMessage("Select a valid decision cycle.");

        RuleFor(x => x.PricePositioning)
            .Must(value => string.IsNullOrWhiteSpace(value) || AllowedPricePositioning.Contains(value))
            .WithMessage("Select a valid price positioning.");

        RuleFor(x => x.AverageCustomerSpendBand)
            .Must(value => string.IsNullOrWhiteSpace(value) || AllowedAverageCustomerSpendBands.Contains(value))
            .WithMessage("Select a valid average customer spend range.");

        RuleFor(x => x.GrowthTarget)
            .Must(value => string.IsNullOrWhiteSpace(value) || AllowedGrowthTargets.Contains(value))
            .WithMessage("Select a valid growth target.");

        RuleFor(x => x.UrgencyLevel)
            .Must(value => string.IsNullOrWhiteSpace(value) || AllowedUrgencyLevels.Contains(value))
            .WithMessage("Select a valid urgency level.");

        RuleFor(x => x.AudienceClarity)
            .Must(value => string.IsNullOrWhiteSpace(value) || AllowedAudienceClarity.Contains(value))
            .WithMessage("Select a valid audience clarity level.");

        RuleFor(x => x.ValuePropositionFocus)
            .Must(value => string.IsNullOrWhiteSpace(value) || AllowedValuePropositionFocus.Contains(value))
            .WithMessage("Select a valid value proposition focus.");

        RuleFor(x => x.TargetAgeMin)
            .GreaterThanOrEqualTo(13)
            .When(x => x.TargetAgeMin.HasValue);

        RuleFor(x => x.TargetAgeMax)
            .LessThanOrEqualTo(100)
            .When(x => x.TargetAgeMax.HasValue);

        RuleFor(x => x)
            .Must(x => !x.TargetAgeMin.HasValue || !x.TargetAgeMax.HasValue || x.TargetAgeMin <= x.TargetAgeMax)
            .WithMessage("Target age range is invalid.");

        RuleFor(x => x)
            .Must(x => !x.TargetLsmMin.HasValue || !x.TargetLsmMax.HasValue || x.TargetLsmMin <= x.TargetLsmMax)
            .WithMessage("Target LSM range is invalid.");

        RuleFor(x => x.AdditionalBudget)
            .GreaterThanOrEqualTo(0)
            .When(x => x.AdditionalBudget.HasValue);

        RuleFor(x => x.MaxMediaItems)
            .GreaterThan(0)
            .When(x => x.MaxMediaItems.HasValue);

        RuleFor(x => x)
            .Must(x => !x.StartDate.HasValue || !x.EndDate.HasValue || x.EndDate >= x.StartDate)
            .WithMessage("Campaign end date must be after the start date.");

        RuleFor(x => x.PreferredVideoAspectRatio)
            .Must(value => string.IsNullOrWhiteSpace(value) || AllowedVideoAspectRatios.Contains(value))
            .WithMessage("Select a valid video aspect ratio.");

        RuleFor(x => x.PreferredVideoDurationSeconds)
            .Must(value => !value.HasValue || AllowedVideoDurations.Contains(value.Value))
            .WithMessage("Select a valid video duration.");
    }
}
