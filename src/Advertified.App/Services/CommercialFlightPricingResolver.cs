using Advertified.App.Contracts.Campaigns;
using Advertified.App.Services.Abstractions;
using Advertified.App.Support;

namespace Advertified.App.Services;

public sealed class CommercialFlightPricingResolver : ICommercialFlightPricingResolver
{
    public RequestedChannelFlight ResolveRequestedFlight(CampaignPlanningRequest request, string? mediaType)
    {
        var normalizedChannel = NormalizeFlightChannel(mediaType);
        var explicitFlight = request.ChannelFlights
            .Where(flight => !string.IsNullOrWhiteSpace(flight.Channel))
            .FirstOrDefault(flight => NormalizeFlightChannel(flight.Channel) == normalizedChannel);

        var startDate = explicitFlight?.StartDate ?? request.StartDate;
        var endDate = explicitFlight?.EndDate ?? request.EndDate;
        var durationWeeks = explicitFlight?.DurationWeeks ?? request.DurationWeeks;
        var durationMonths = explicitFlight?.DurationMonths;

        if (!durationMonths.HasValue && durationWeeks.HasValue && durationWeeks.Value > 0)
        {
            durationMonths = (int)Math.Ceiling(durationWeeks.Value / 4m);
        }

        if (!durationWeeks.HasValue && durationMonths.HasValue && durationMonths.Value > 0)
        {
            durationWeeks = durationMonths.Value * 4;
        }

        if (startDate.HasValue && !endDate.HasValue && durationWeeks.HasValue && durationWeeks.Value > 0)
        {
            endDate = startDate.Value.AddDays((durationWeeks.Value * 7) - 1);
        }

        if (!startDate.HasValue && endDate.HasValue && durationWeeks.HasValue && durationWeeks.Value > 0)
        {
            startDate = endDate.Value.AddDays(-(durationWeeks.Value * 7) + 1);
        }

        var requestedMonthsEquivalent = durationMonths.HasValue && durationMonths.Value > 0
            ? durationMonths.Value
            : durationWeeks.HasValue && durationWeeks.Value > 0
                ? decimal.Round(durationWeeks.Value / 4m, 2, MidpointRounding.AwayFromZero)
                : startDate.HasValue && endDate.HasValue && endDate.Value >= startDate.Value
                    ? decimal.Round((endDate.Value.DayNumber - startDate.Value.DayNumber + 1) / 30m, 2, MidpointRounding.AwayFromZero)
                    : 1m;

        if (requestedMonthsEquivalent <= 0m)
        {
            requestedMonthsEquivalent = 1m;
        }

        return new RequestedChannelFlight(
            normalizedChannel,
            startDate,
            endDate,
            durationWeeks,
            durationMonths,
            requestedMonthsEquivalent,
            BuildDurationLabel(durationMonths, durationWeeks, startDate, endDate));
    }

    public CommercialPriceResolution Resolve(
        CampaignPlanningRequest request,
        string? mediaType,
        string pricingModel,
        decimal markedUpRawCost,
        decimal markedUpComparableMonthlyCost,
        int? offerDurationWeeks,
        int? offerDurationMonths,
        bool packageOnly,
        bool allowsProration)
    {
        var requestedFlight = ResolveRequestedFlight(request, mediaType);
        var requestedMonthsEquivalent = requestedFlight.RequestedMonthsEquivalent <= 0m ? 1m : requestedFlight.RequestedMonthsEquivalent;
        var effectiveComparableMonthly = markedUpComparableMonthlyCost > 0m ? markedUpComparableMonthlyCost : markedUpRawCost;
        var normalizedPricingModel = (pricingModel ?? string.Empty).Trim().ToLowerInvariant();
        var offerMonthsEquivalent = ResolveOfferMonthsEquivalent(offerDurationMonths, offerDurationWeeks);

        decimal quotedCost;
        decimal appliedMonthsEquivalent;
        string appliedDurationLabel;
        string explanation;
        bool exactDurationMatch;

        if (effectiveComparableMonthly <= 0m)
        {
            effectiveComparableMonthly = markedUpRawCost;
        }

        if (allowsProration || normalizedPricingModel.Contains("monthly", StringComparison.OrdinalIgnoreCase))
        {
            quotedCost = decimal.Round(effectiveComparableMonthly * requestedMonthsEquivalent, 2, MidpointRounding.AwayFromZero);
            appliedMonthsEquivalent = requestedMonthsEquivalent;
            appliedDurationLabel = requestedFlight.DurationLabel;
            explanation = $"Priced against the requested {requestedFlight.DurationLabel.ToLowerInvariant()} flight.";
            exactDurationMatch = true;
        }
        else if (offerMonthsEquivalent > 0m)
        {
            var units = Math.Max(1m, Math.Ceiling(requestedMonthsEquivalent / offerMonthsEquivalent));
            quotedCost = decimal.Round(markedUpRawCost * units, 2, MidpointRounding.AwayFromZero);
            appliedMonthsEquivalent = decimal.Round(offerMonthsEquivalent * units, 2, MidpointRounding.AwayFromZero);
            appliedDurationLabel = BuildAppliedDurationLabel(offerDurationMonths, offerDurationWeeks, units);
            exactDurationMatch = Math.Abs(appliedMonthsEquivalent - requestedMonthsEquivalent) < 0.01m;
            explanation = exactDurationMatch
                ? $"Exact package match for the requested {requestedFlight.DurationLabel.ToLowerInvariant()}."
                : $"Supplier sells this as {appliedDurationLabel.ToLowerInvariant()}, so the quote uses the lowest valid package.";
        }
        else if (packageOnly)
        {
            quotedCost = markedUpRawCost;
            appliedMonthsEquivalent = requestedMonthsEquivalent;
            appliedDurationLabel = requestedFlight.DurationLabel;
            explanation = "Supplier package has no explicit duration metadata, so the quote uses the package total.";
            exactDurationMatch = false;
        }
        else
        {
            quotedCost = decimal.Round(effectiveComparableMonthly * requestedMonthsEquivalent, 2, MidpointRounding.AwayFromZero);
            appliedMonthsEquivalent = requestedMonthsEquivalent;
            appliedDurationLabel = requestedFlight.DurationLabel;
            explanation = $"Priced using the normalized monthly equivalent for the requested {requestedFlight.DurationLabel.ToLowerInvariant()}.";
            exactDurationMatch = true;
        }

        var durationFitScore = exactDurationMatch
            ? 100m
            : appliedMonthsEquivalent <= 0m || requestedMonthsEquivalent <= 0m
                ? 40m
                : Math.Max(20m, decimal.Round(100m - (((appliedMonthsEquivalent / requestedMonthsEquivalent) - 1m) * 40m), 2, MidpointRounding.AwayFromZero));
        var commercialPenalty = decimal.Round(100m - durationFitScore, 2, MidpointRounding.AwayFromZero);
        var resolvedEndDate = ResolveEndDate(requestedFlight.StartDate, requestedFlight.EndDate, appliedMonthsEquivalent, offerDurationWeeks);

        return new CommercialPriceResolution(
            QuotedCost: quotedCost,
            ComparableMonthlyCost: effectiveComparableMonthly,
            DurationFitScore: durationFitScore,
            CommercialPenalty: commercialPenalty,
            AppliedPricingModel: string.IsNullOrWhiteSpace(pricingModel) ? (packageOnly ? "package_total" : "monthly_equivalent") : pricingModel.Trim(),
            RequestedDurationLabel: requestedFlight.DurationLabel,
            AppliedDurationLabel: appliedDurationLabel,
            RequestedMonthsEquivalent: requestedMonthsEquivalent,
            AppliedMonthsEquivalent: appliedMonthsEquivalent,
            RequestedStartDate: requestedFlight.StartDate,
            RequestedEndDate: requestedFlight.EndDate,
            ResolvedStartDate: requestedFlight.StartDate,
            ResolvedEndDate: resolvedEndDate,
            Explanation: explanation,
            ExactDurationMatch: exactDurationMatch);
    }

    private static decimal ResolveOfferMonthsEquivalent(int? offerDurationMonths, int? offerDurationWeeks)
    {
        if (offerDurationMonths.HasValue && offerDurationMonths.Value > 0)
        {
            return offerDurationMonths.Value;
        }

        if (offerDurationWeeks.HasValue && offerDurationWeeks.Value > 0)
        {
            return decimal.Round(offerDurationWeeks.Value / 4m, 2, MidpointRounding.AwayFromZero);
        }

        return 0m;
    }

    private static DateOnly? ResolveEndDate(DateOnly? startDate, DateOnly? requestedEndDate, decimal appliedMonthsEquivalent, int? offerDurationWeeks)
    {
        if (requestedEndDate.HasValue)
        {
            return requestedEndDate;
        }

        if (!startDate.HasValue)
        {
            return null;
        }

        if (offerDurationWeeks.HasValue && offerDurationWeeks.Value > 0)
        {
            return startDate.Value.AddDays((offerDurationWeeks.Value * 7) - 1);
        }

        var wholeDays = Math.Max(1, (int)Math.Ceiling(appliedMonthsEquivalent * 30m));
        return startDate.Value.AddDays(wholeDays - 1);
    }

    private static string BuildDurationLabel(int? durationMonths, int? durationWeeks, DateOnly? startDate, DateOnly? endDate)
    {
        if (durationMonths.HasValue && durationMonths.Value > 0)
        {
            return durationMonths.Value == 1 ? "1 month" : $"{durationMonths.Value} months";
        }

        if (durationWeeks.HasValue && durationWeeks.Value > 0)
        {
            return durationWeeks.Value == 1 ? "1 week" : $"{durationWeeks.Value} weeks";
        }

        if (startDate.HasValue && endDate.HasValue && endDate.Value >= startDate.Value)
        {
            var totalDays = endDate.Value.DayNumber - startDate.Value.DayNumber + 1;
            var weeks = Math.Max(1, (int)Math.Ceiling(totalDays / 7m));
            return weeks == 1 ? "1 week" : $"{weeks} weeks";
        }

        return "1 month";
    }

    private static string BuildAppliedDurationLabel(int? offerDurationMonths, int? offerDurationWeeks, decimal units)
    {
        if (offerDurationMonths.HasValue && offerDurationMonths.Value > 0)
        {
            var totalMonths = decimal.Round(offerDurationMonths.Value * units, 2, MidpointRounding.AwayFromZero);
            return totalMonths == 1m ? "1 month" : $"{TrimTrailingZeros(totalMonths)} months";
        }

        if (offerDurationWeeks.HasValue && offerDurationWeeks.Value > 0)
        {
            var totalWeeks = decimal.Round(offerDurationWeeks.Value * units, 2, MidpointRounding.AwayFromZero);
            return totalWeeks == 1m ? "1 week" : $"{TrimTrailingZeros(totalWeeks)} weeks";
        }

        return units == 1m ? "1 package" : $"{TrimTrailingZeros(units)} packages";
    }

    private static string TrimTrailingZeros(decimal value)
    {
        return value.ToString(value % 1m == 0m ? "0" : "0.##");
    }

    private static string NormalizeFlightChannel(string? mediaType)
    {
        if (PlanningChannelSupport.IsOohFamilyChannel(mediaType))
        {
            return PlanningChannelSupport.OohAlias;
        }

        return PlanningChannelSupport.NormalizeChannel(mediaType);
    }
}
