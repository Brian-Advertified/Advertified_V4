using Advertified.App.Contracts.Campaigns;
using FluentValidation;

namespace Advertified.App.Validation;

public sealed class CampaignPlanningRequestValidator : AbstractValidator<CampaignPlanningRequest>
{
    private static readonly HashSet<string> AllowedGeographyScopes = new(StringComparer.OrdinalIgnoreCase)
    {
        "local",
        "provincial",
        "national"
    };

    private static readonly HashSet<string> AllowedMediaTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "ooh",
        "radio",
        "radio_slot",
        "radio_package",
        "tv",
        "digital"
    };

    public CampaignPlanningRequestValidator()
    {
        RuleFor(x => x.CampaignId)
            .NotEmpty()
            .WithMessage("Campaign ID is required.");

        RuleFor(x => x.SelectedBudget)
            .GreaterThan(0)
            .WithMessage("Selected budget must be greater than zero.");

        RuleFor(x => x.GeographyScope)
            .Must(value => string.IsNullOrWhiteSpace(value) || AllowedGeographyScopes.Contains(value))
            .WithMessage("Select a valid geography scope.");

        RuleFor(x => x)
            .Must(x => !x.TargetLsmMin.HasValue || !x.TargetLsmMax.HasValue || x.TargetLsmMin <= x.TargetLsmMax)
            .WithMessage("Target LSM range is invalid.");

        RuleFor(x => x.AdditionalBudget)
            .GreaterThanOrEqualTo(0)
            .When(x => x.AdditionalBudget.HasValue);

        RuleFor(x => x.MaxMediaItems)
            .GreaterThan(0)
            .When(x => x.MaxMediaItems.HasValue);

        RuleForEach(x => x.PreferredMediaTypes)
            .Must(value => AllowedMediaTypes.Contains(value))
            .WithMessage("Preferred media types contain an unsupported value.");

        RuleForEach(x => x.ExcludedMediaTypes)
            .Must(value => AllowedMediaTypes.Contains(value))
            .WithMessage("Excluded media types contain an unsupported value.");
    }
}
