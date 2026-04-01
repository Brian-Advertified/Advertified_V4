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
        "regional",
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
