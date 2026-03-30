using Advertified.App.Data.Entities;

namespace Advertified.App.Support;

public static class CampaignOperationsPolicy
{
    public static bool IsOrderOperationallyActive(PackageOrder order)
    {
        return string.Equals(order.PaymentStatus, CampaignStatuses.Paid, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(order.RefundStatus, "refunded", StringComparison.OrdinalIgnoreCase);
    }

    public static RefundPolicySnapshot BuildRefundSnapshot(Campaign campaign)
    {
        return BuildRefundSnapshot(
            campaign.Status,
            campaign.PackageOrder.Amount,
            campaign.PackageOrder.RefundedAmount,
            campaign.PackageOrder.GatewayFeeRetainedAmount,
            campaign.PackageOrder.SelectedBudget);
    }

    public static RefundPolicySnapshot BuildRefundSnapshot(
        string campaignStatus,
        decimal chargedTotal,
        decimal refundedAmount,
        decimal gatewayFeeRetainedAmount,
        decimal? selectedBudget)
    {
        var resolvedSelectedBudget = selectedBudget ?? chargedTotal;
        var remainingCollectedAmount = Math.Max(0m, chargedTotal - refundedAmount);
        gatewayFeeRetainedAmount = Math.Max(0m, gatewayFeeRetainedAmount);

        if (remainingCollectedAmount <= 0m)
        {
            return new RefundPolicySnapshot(
                Stage: "refunded",
                Label: "Refund already completed",
                Summary: "No collected amount remains on this order.",
                SuggestedRefundAmount: 0m,
                MaxManualRefundAmount: 0m,
                RemainingCollectedAmount: 0m);
        }

        if (string.Equals(campaignStatus, CampaignStatuses.Paid, StringComparison.OrdinalIgnoreCase))
        {
            var suggested = Math.Max(0m, remainingCollectedAmount - gatewayFeeRetainedAmount);
            return new RefundPolicySnapshot(
                Stage: "before_work_starts",
                Label: "Full refund before work starts",
                Summary: gatewayFeeRetainedAmount > 0m
                    ? "Refund the full collected amount less the retained non-recoverable gateway fee."
                    : "No campaign work has started yet, so a full refund is available.",
                SuggestedRefundAmount: suggested,
                MaxManualRefundAmount: remainingCollectedAmount,
                RemainingCollectedAmount: remainingCollectedAmount);
        }

        if (IsStrategyInProgressStatus(campaignStatus))
        {
            var suggested = Math.Min(remainingCollectedAmount, Math.Max(0m, resolvedSelectedBudget - refundedAmount));
            return new RefundPolicySnapshot(
                Stage: "strategy_in_progress",
                Label: "Partial refund with AI studio retained",
                Summary: "Planning work is already in motion, so the AI studio and strategy reserve should be retained while the balance can be refunded.",
                SuggestedRefundAmount: suggested,
                MaxManualRefundAmount: remainingCollectedAmount,
                RemainingCollectedAmount: remainingCollectedAmount);
        }

        return new RefundPolicySnapshot(
            Stage: "post_delivery_or_live",
            Label: "Manual unused-value refund only",
            Summary: "Recommendation or creative delivery has already happened, so only unused or uncommitted value should be refunded after manual review.",
            SuggestedRefundAmount: 0m,
            MaxManualRefundAmount: remainingCollectedAmount,
            RemainingCollectedAmount: remainingCollectedAmount);
    }

    public static CampaignScheduleSnapshot BuildScheduleSnapshot(Campaign campaign, DateOnly today)
    {
        return BuildScheduleSnapshot(
            campaign.CampaignBrief?.StartDate,
            campaign.CampaignBrief?.EndDate,
            campaign.CampaignBrief?.DurationWeeks,
            campaign.CampaignSupplierBookings.Where(x => x.LiveFrom.HasValue).Select(x => x.LiveFrom).Min(),
            campaign.CampaignSupplierBookings.Where(x => x.LiveTo.HasValue).Select(x => x.LiveTo).Max(),
            campaign.CampaignPauseWindows.Sum(x => x.PausedDayCount),
            campaign.TotalPausedDays,
            campaign.PausedAt,
            today);
    }

    public static CampaignScheduleSnapshot BuildScheduleSnapshot(
        DateOnly? startDate,
        DateOnly? endDate,
        int? durationWeeks,
        DateOnly? bookedStart,
        DateOnly? bookedEnd,
        int pausedDaysFromWindows,
        int totalPausedDays,
        DateTime? pausedAt,
        DateOnly today)
    {
        if (startDate is null && durationWeeks.HasValue && durationWeeks.Value > 0 && endDate is not null)
        {
            startDate = endDate.Value.AddDays(-(durationWeeks.Value * 7) + 1);
        }

        if (endDate is null && startDate is not null && durationWeeks.HasValue && durationWeeks.Value > 0)
        {
            endDate = startDate.Value.AddDays((durationWeeks.Value * 7) - 1);
        }

        startDate = bookedStart ?? startDate;
        endDate = bookedEnd ?? endDate;

        if (startDate is null && endDate is null)
        {
            return new CampaignScheduleSnapshot(null, null, null, null);
        }

        if (endDate is null)
        {
            return new CampaignScheduleSnapshot(startDate, null, null, null);
        }

        var effectivePausedDays = pausedDaysFromWindows;
        if (effectivePausedDays < totalPausedDays)
        {
            effectivePausedDays = totalPausedDays;
        }

        if (pausedAt.HasValue)
        {
            var pausedDate = DateOnly.FromDateTime(pausedAt.Value);
            if (today > pausedDate)
            {
                effectivePausedDays += today.DayNumber - pausedDate.DayNumber;
            }
        }

        var effectiveEndDate = endDate.Value.AddDays(effectivePausedDays);
        var daysLeft = effectiveEndDate.DayNumber - today.DayNumber + 1;
        return new CampaignScheduleSnapshot(
            StartDate: startDate,
            EndDate: endDate,
            EffectiveEndDate: effectiveEndDate,
            DaysLeft: daysLeft > 0 ? daysLeft : 0);
    }

    public sealed record RefundPolicySnapshot(
        string Stage,
        string Label,
        string Summary,
        decimal SuggestedRefundAmount,
        decimal MaxManualRefundAmount,
        decimal RemainingCollectedAmount);

    public sealed record CampaignScheduleSnapshot(
        DateOnly? StartDate,
        DateOnly? EndDate,
        DateOnly? EffectiveEndDate,
        int? DaysLeft);

    private static bool IsStrategyInProgressStatus(string campaignStatus)
    {
        return campaignStatus switch
        {
            CampaignStatuses.BriefInProgress => true,
            CampaignStatuses.BriefSubmitted => true,
            CampaignStatuses.PlanningInProgress => true,
            CampaignStatuses.ReviewReady => true,
            _ => false
        };
    }
}
