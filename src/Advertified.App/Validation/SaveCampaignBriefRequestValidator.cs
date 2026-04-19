using Advertified.App.Contracts.Campaigns;
using Advertified.App.Services;
using Advertified.App.Support;
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

    public SaveCampaignBriefRequestValidator(FormOptionsService formOptionsService)
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
            .MustAsync((value, cancellationToken) => formOptionsService.IsAllowedValueAsync(FormOptionSetKeys.BusinessStages, value, cancellationToken))
            .WithMessage("Select a valid business stage.");

        RuleFor(x => x.MonthlyRevenueBand)
            .MustAsync((value, cancellationToken) => formOptionsService.IsAllowedValueAsync(FormOptionSetKeys.MonthlyRevenueBands, value, cancellationToken))
            .WithMessage("Select a valid monthly revenue band.");

        RuleFor(x => x.SalesModel)
            .MustAsync((value, cancellationToken) => formOptionsService.IsAllowedValueAsync(FormOptionSetKeys.SalesModels, value, cancellationToken))
            .WithMessage("Select a valid sales model.");

        RuleFor(x => x.CustomerType)
            .MustAsync((value, cancellationToken) => formOptionsService.IsAllowedValueAsync(FormOptionSetKeys.CustomerTypes, value, cancellationToken))
            .WithMessage("Select a valid customer type.");

        RuleFor(x => x.BuyingBehaviour)
            .MustAsync((value, cancellationToken) => formOptionsService.IsAllowedValueAsync(FormOptionSetKeys.BuyingBehaviours, value, cancellationToken))
            .WithMessage("Select a valid buying behaviour.");

        RuleFor(x => x.DecisionCycle)
            .MustAsync((value, cancellationToken) => formOptionsService.IsAllowedValueAsync(FormOptionSetKeys.DecisionCycles, value, cancellationToken))
            .WithMessage("Select a valid decision cycle.");

        RuleFor(x => x.PricePositioning)
            .MustAsync((value, cancellationToken) => formOptionsService.IsAllowedValueAsync(FormOptionSetKeys.PricePositioning, value, cancellationToken))
            .WithMessage("Select a valid price positioning.");

        RuleFor(x => x.AverageCustomerSpendBand)
            .MustAsync((value, cancellationToken) => formOptionsService.IsAllowedValueAsync(FormOptionSetKeys.AverageCustomerSpendBands, value, cancellationToken))
            .WithMessage("Select a valid average customer spend range.");

        RuleFor(x => x.GrowthTarget)
            .MustAsync((value, cancellationToken) => formOptionsService.IsAllowedValueAsync(FormOptionSetKeys.GrowthTargets, value, cancellationToken))
            .WithMessage("Select a valid growth target.");

        RuleFor(x => x.UrgencyLevel)
            .MustAsync((value, cancellationToken) => formOptionsService.IsAllowedValueAsync(FormOptionSetKeys.UrgencyLevels, value, cancellationToken))
            .WithMessage("Select a valid urgency level.");

        RuleFor(x => x.AudienceClarity)
            .MustAsync((value, cancellationToken) => formOptionsService.IsAllowedValueAsync(FormOptionSetKeys.AudienceClarity, value, cancellationToken))
            .WithMessage("Select a valid audience clarity level.");

        RuleFor(x => x.ValuePropositionFocus)
            .MustAsync((value, cancellationToken) => formOptionsService.IsAllowedValueAsync(FormOptionSetKeys.ValuePropositionFocus, value, cancellationToken))
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

        RuleForEach(x => x.ChannelFlights)
            .ChildRules(flight =>
            {
                flight.RuleFor(x => x.Channel)
                    .NotEmpty()
                    .Must(channel =>
                    {
                        var normalized = CampaignFlightingSupport.NormalizeChannel(channel);
                        return normalized is PlanningChannelSupport.OohAlias or PlanningChannelSupport.Radio or PlanningChannelSupport.Tv or PlanningChannelSupport.Digital;
                    })
                    .WithMessage("Select a valid channel flight.");

                flight.RuleFor(x => x.DurationWeeks)
                    .GreaterThan(0)
                    .When(x => x.DurationWeeks.HasValue);

                flight.RuleFor(x => x.DurationMonths)
                    .GreaterThan(0)
                    .When(x => x.DurationMonths.HasValue);

                flight.RuleFor(x => x)
                    .Must(x => !x.StartDate.HasValue || !x.EndDate.HasValue || x.EndDate >= x.StartDate)
                    .WithMessage("Channel flight end date must be after the start date.");
            });

        RuleFor(x => x)
            .Must(x => x.ChannelFlights is null
                || x.ChannelFlights
                    .Where(flight => !string.IsNullOrWhiteSpace(flight.Channel))
                    .Select(flight => CampaignFlightingSupport.NormalizeChannel(flight.Channel))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Count() == x.ChannelFlights.Count(flight => !string.IsNullOrWhiteSpace(flight.Channel)))
            .WithMessage("Each channel can only have one active flight.");

        RuleFor(x => x.PreferredVideoAspectRatio)
            .Must(value => string.IsNullOrWhiteSpace(value) || AllowedVideoAspectRatios.Contains(value))
            .WithMessage("Select a valid video aspect ratio.");

        RuleFor(x => x.PreferredVideoDurationSeconds)
            .Must(value => !value.HasValue || AllowedVideoDurations.Contains(value.Value))
            .WithMessage("Select a valid video duration.");

        RuleFor(x => x.TargetLatitude)
            .InclusiveBetween(-90d, 90d)
            .When(x => x.TargetLatitude.HasValue);

        RuleFor(x => x.TargetLongitude)
            .InclusiveBetween(-180d, 180d)
            .When(x => x.TargetLongitude.HasValue);

        RuleFor(x => x)
            .Must(x => x.TargetLatitude.HasValue == x.TargetLongitude.HasValue)
            .WithMessage("Target coordinates must include both latitude and longitude.");
    }
}
