using Advertified.App.Contracts.Campaigns;
using Advertified.App.Services.Abstractions;
using Advertified.App.Support;

namespace Advertified.App.Services;

public sealed class CommercialFlightPricingResolver : ICommercialFlightPricingResolver
{
    public CommercialPriceResolution Resolve(
        CampaignPlanningRequest request,
        string mediaType,
        string pricingModel,
        decimal quotedBaseCost,
        decimal comparableMonthlyCost,
        int? offerDurationWeeks,
        int? offerDurationMonths,
        bool packageOnly,
        bool allowsProration)
    {
        var requestedFlight = ResolveRequestedFlight(request, mediaType);
        var requestedMonths = requestedFlight.MonthsEquivalent;
        var appliedMonths = ResolveAppliedMonthsEquivalent(offerDurationWeeks, offerDurationMonths, comparableMonthlyCost, quotedBaseCost, requestedMonths, packageOnly);
        var effectivePricingModel = string.IsNullOrWhiteSpace(pricingModel)
            ? (packageOnly ? "package_total" : "monthly_equivalent")
            : pricingModel.Trim();

        decimal quotedCost;
        string explanation;
        string appliedDurationLabel;
        decimal durationFitScore;

        if (packageOnly)
        {
            if (allowsProration && requestedMonths > 0m)
            {
                quotedCost = RoundCurrency(comparableMonthlyCost * requestedMonths);
                appliedDurationLabel = requestedFlight.DurationLabel;
                durationFitScore = 1.0m;
                explanation = $"Priced using {effectivePricingModel} at the requested flight length.";
            }
            else
            {
                quotedCost = RoundCurrency(quotedBaseCost);
                appliedDurationLabel = BuildDurationLabel(offerDurationWeeks, offerDurationMonths)
                    ?? requestedFlight.DurationLabel;
                durationFitScore = ScoreDurationFit(requestedMonths, appliedMonths, allowsProration);
                explanation = BuildPackageExplanation(requestedMonths, appliedMonths, requestedFlight.DurationLabel, appliedDurationLabel, allowsProration);
            }
        }
        else
        {
            var monthsToQuote = requestedMonths > 0m ? requestedMonths : 1m;
            quotedCost = RoundCurrency(comparableMonthlyCost * monthsToQuote);
            appliedDurationLabel = requestedFlight.DurationLabel;
            durationFitScore = requestedMonths > 0m ? 1.0m : 0.9m;
            explanation = "Priced using monthly-equivalent inventory for the requested flight.";
        }

        var commercialPenalty = RoundPenalty(1.0m - durationFitScore);
        var resolvedStart = requestedFlight.StartDate;
        var resolvedEnd = requestedFlight.EndDate ?? ResolveEndDate(requestedFlight.StartDate, requestedFlight.DurationWeeks, requestedFlight.DurationMonths, offerDurationWeeks, offerDurationMonths, allowsProration, packageOnly);

        return new CommercialPriceResolution(
            QuotedCost: quotedCost,
            ComparableMonthlyCost: RoundCurrency(comparableMonthlyCost),
            AppliedPricingModel: effectivePricingModel,
            RequestedDurationLabel: requestedFlight.DurationLabel,
            AppliedDurationLabel: appliedDurationLabel,
            RequestedMonthsEquivalent: requestedMonths,
            AppliedMonthsEquivalent: appliedMonths,
            DurationFitScore: durationFitScore,
            CommercialPenalty: commercialPenalty,
            Explanation: explanation,
            RequestedStartDate: requestedFlight.StartDate,
            RequestedEndDate: requestedFlight.EndDate,
            ResolvedStartDate: resolvedStart,
            ResolvedEndDate: resolvedEnd);
    }

    private static RequestedFlight ResolveRequestedFlight(CampaignPlanningRequest request, string mediaType)
    {
        var normalizedMediaType = PlanningChannelSupport.NormalizeChannel(mediaType);
        var match = request.ChannelFlights
            .FirstOrDefault(flight => MatchesFlight(flight.Channel, normalizedMediaType));

        var start = match?.StartDate ?? request.StartDate;
        var end = match?.EndDate ?? request.EndDate;
        var durationWeeks = match?.DurationWeeks ?? request.DurationWeeks;
        var durationMonths = match?.DurationMonths;

        if (!durationWeeks.HasValue && start.HasValue && end.HasValue && end.Value >= start.Value)
        {
            durationWeeks = Math.Max(1, (int)Math.Ceiling((end.Value.DayNumber - start.Value.DayNumber + 1) / 7d));
        }

        if (!durationMonths.HasValue && start.HasValue && end.HasValue && end.Value >= start.Value)
        {
            durationMonths = Math.Max(1, ((end.Value.Year - start.Value.Year) * 12) + end.Value.Month - start.Value.Month + (end.Value.Day >= start.Value.Day ? 1 : 0));
        }

        var monthsEquivalent = ResolveMonthsEquivalent(durationWeeks, durationMonths);
        var durationLabel = BuildDurationLabel(durationWeeks, durationMonths)
            ?? "Campaign duration";

        return new RequestedFlight(start, end, durationWeeks, durationMonths, monthsEquivalent, durationLabel);
    }

    private static bool MatchesFlight(string? flightChannel, string normalizedMediaType)
    {
        var normalizedFlight = PlanningChannelSupport.NormalizeChannel(flightChannel);
        if (normalizedFlight == normalizedMediaType)
        {
            return true;
        }

        return normalizedFlight == PlanningChannelSupport.OohAlias
            && PlanningChannelSupport.IsOohFamilyChannel(normalizedMediaType);
    }

    private static decimal ResolveAppliedMonthsEquivalent(
        int? offerDurationWeeks,
        int? offerDurationMonths,
        decimal comparableMonthlyCost,
        decimal quotedBaseCost,
        decimal requestedMonths,
        bool packageOnly)
    {
        if (offerDurationMonths.HasValue && offerDurationMonths.Value > 0)
        {
            return offerDurationMonths.Value;
        }

        if (offerDurationWeeks.HasValue && offerDurationWeeks.Value > 0)
        {
            return Math.Round(offerDurationWeeks.Value / 4.345m, 2, MidpointRounding.AwayFromZero);
        }

        if (packageOnly && comparableMonthlyCost > 0m && quotedBaseCost > 0m)
        {
            return Math.Round(quotedBaseCost / comparableMonthlyCost, 2, MidpointRounding.AwayFromZero);
        }

        return requestedMonths > 0m ? requestedMonths : 1m;
    }

    private static decimal ResolveMonthsEquivalent(int? durationWeeks, int? durationMonths)
    {
        if (durationMonths.HasValue && durationMonths.Value > 0)
        {
            return durationMonths.Value;
        }

        if (durationWeeks.HasValue && durationWeeks.Value > 0)
        {
            return Math.Round(durationWeeks.Value / 4.345m, 2, MidpointRounding.AwayFromZero);
        }

        return 1m;
    }

    private static string? BuildDurationLabel(int? durationWeeks, int? durationMonths)
    {
        if (durationMonths.HasValue && durationMonths.Value > 0)
        {
            return durationMonths.Value == 1 ? "1 month" : $"{durationMonths.Value} months";
        }

        if (durationWeeks.HasValue && durationWeeks.Value > 0)
        {
            return durationWeeks.Value == 1 ? "1 week" : $"{durationWeeks.Value} weeks";
        }

        return null;
    }

    private static decimal ScoreDurationFit(decimal requestedMonths, decimal appliedMonths, bool allowsProration)
    {
        if (requestedMonths <= 0m || appliedMonths <= 0m)
        {
            return 0.85m;
        }

        if (allowsProration)
        {
            return 1.0m;
        }

        var ratio = requestedMonths / appliedMonths;
        if (ratio >= 1m)
        {
            return 1.0m;
        }

        return Math.Clamp(Math.Round(ratio, 2, MidpointRounding.AwayFromZero), 0.25m, 1.0m);
    }

    private static string BuildPackageExplanation(
        decimal requestedMonths,
        decimal appliedMonths,
        string requestedLabel,
        string appliedLabel,
        bool allowsProration)
    {
        if (allowsProration)
        {
            return $"Priced proportionally for the requested {requestedLabel}.";
        }

        if (requestedMonths > 0m && appliedMonths > requestedMonths)
        {
            return $"Requested {requestedLabel}; supplier sells this as a {appliedLabel} fixed package, so the quote uses the full package price.";
        }

        return $"Priced using the supplier's {appliedLabel} package terms.";
    }

    private static DateOnly? ResolveEndDate(
        DateOnly? startDate,
        int? requestedDurationWeeks,
        int? requestedDurationMonths,
        int? offerDurationWeeks,
        int? offerDurationMonths,
        bool allowsProration,
        bool packageOnly)
    {
        if (!startDate.HasValue)
        {
            return null;
        }

        var durationMonths = allowsProration ? requestedDurationMonths : (packageOnly ? offerDurationMonths ?? requestedDurationMonths : requestedDurationMonths);
        if (durationMonths.HasValue && durationMonths.Value > 0)
        {
            return startDate.Value.AddMonths(durationMonths.Value).AddDays(-1);
        }

        var durationWeeks = allowsProration ? requestedDurationWeeks : (packageOnly ? offerDurationWeeks ?? requestedDurationWeeks : requestedDurationWeeks);
        if (durationWeeks.HasValue && durationWeeks.Value > 0)
        {
            return startDate.Value.AddDays((durationWeeks.Value * 7) - 1);
        }

        return null;
    }

    private static decimal RoundCurrency(decimal value)
    {
        return decimal.Round(value, 2, MidpointRounding.AwayFromZero);
    }

    private static decimal RoundPenalty(decimal value)
    {
        return decimal.Round(value, 3, MidpointRounding.AwayFromZero);
    }

    private sealed record RequestedFlight(
        DateOnly? StartDate,
        DateOnly? EndDate,
        int? DurationWeeks,
        int? DurationMonths,
        decimal MonthsEquivalent,
        string DurationLabel);
}
