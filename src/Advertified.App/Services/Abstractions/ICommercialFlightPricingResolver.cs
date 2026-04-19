using Advertified.App.Contracts.Campaigns;

namespace Advertified.App.Services.Abstractions;

public interface ICommercialFlightPricingResolver
{
    RequestedChannelFlight ResolveRequestedFlight(CampaignPlanningRequest request, string? mediaType);

    CommercialPriceResolution Resolve(
        CampaignPlanningRequest request,
        string? mediaType,
        string pricingModel,
        decimal markedUpRawCost,
        decimal markedUpComparableMonthlyCost,
        int? offerDurationWeeks,
        int? offerDurationMonths,
        bool packageOnly,
        bool allowsProration);
}

public sealed record RequestedChannelFlight(
    string Channel,
    DateOnly? StartDate,
    DateOnly? EndDate,
    int? DurationWeeks,
    int? DurationMonths,
    decimal RequestedMonthsEquivalent,
    string DurationLabel);

public sealed record CommercialPriceResolution(
    decimal QuotedCost,
    decimal ComparableMonthlyCost,
    decimal DurationFitScore,
    decimal CommercialPenalty,
    string AppliedPricingModel,
    string RequestedDurationLabel,
    string AppliedDurationLabel,
    decimal RequestedMonthsEquivalent,
    decimal AppliedMonthsEquivalent,
    DateOnly? RequestedStartDate,
    DateOnly? RequestedEndDate,
    DateOnly? ResolvedStartDate,
    DateOnly? ResolvedEndDate,
    string Explanation,
    bool ExactDurationMatch);
