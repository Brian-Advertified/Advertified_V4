using Advertified.App.Contracts.Campaigns;

namespace Advertified.App.Services.Abstractions;

public interface ICommercialFlightPricingResolver
{
    CommercialPriceResolution Resolve(
        CampaignPlanningRequest request,
        string mediaType,
        string pricingModel,
        decimal quotedBaseCost,
        decimal comparableMonthlyCost,
        int? offerDurationWeeks,
        int? offerDurationMonths,
        bool packageOnly,
        bool allowsProration);
}

public sealed record CommercialPriceResolution(
    decimal QuotedCost,
    decimal ComparableMonthlyCost,
    string AppliedPricingModel,
    string RequestedDurationLabel,
    string AppliedDurationLabel,
    decimal RequestedMonthsEquivalent,
    decimal AppliedMonthsEquivalent,
    decimal DurationFitScore,
    decimal CommercialPenalty,
    string Explanation,
    DateOnly? RequestedStartDate,
    DateOnly? RequestedEndDate,
    DateOnly? ResolvedStartDate,
    DateOnly? ResolvedEndDate);
