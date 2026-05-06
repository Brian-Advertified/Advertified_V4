using System;

namespace Advertified.App.Support;

public static class PricingPolicy
{
    public static decimal CalculateAiStudioReserveAmount(decimal selectedBudget, decimal aiStudioReservePercent)
    {
        if (selectedBudget <= 0m)
        {
            return 0m;
        }

        return Math.Round(selectedBudget * Math.Max(0m, aiStudioReservePercent), 2, MidpointRounding.AwayFromZero);
    }

    public static decimal CalculateAiStudioReserveAmount(decimal selectedBudget, decimal aiStudioReservePercent, string? packageCode, string? packageName)
    {
        if (!IncludesAiCreative(packageCode, packageName))
        {
            return 0m;
        }

        return CalculateAiStudioReserveAmount(selectedBudget, aiStudioReservePercent);
    }

    public static decimal CalculateChargedAmount(decimal selectedBudget, decimal aiStudioReservePercent)
    {
        if (selectedBudget <= 0m)
        {
            return 0m;
        }

        return Math.Round(selectedBudget, 2, MidpointRounding.AwayFromZero);
    }

    public static decimal ResolvePlanningBudget(decimal selectedBudget, decimal aiStudioReserveAmount)
    {
        if (selectedBudget <= 0m)
        {
            return 0m;
        }

        // Planning and recommendation views should use the full selected budget.
        // The reserve is still tracked on the order, but no longer deducted from planning totals.
        _ = aiStudioReserveAmount;
        return Math.Round(selectedBudget, 2, MidpointRounding.AwayFromZero);
    }

    public static bool IncludesAiCreative(string? packageCode, string? packageName)
    {
        if (!string.IsNullOrWhiteSpace(packageCode)
            && packageCode.Trim().Equals("launch", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(packageName)
            && packageName.Contains("launch", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }

    public static decimal ResolveMarkupPercent(string mediaType, string? subtype, PricingSettingsSnapshot settings)
    {
        var normalizedMediaType = PlanningChannelSupport.NormalizeChannel(mediaType);
        var normalizedSubtype = subtype?.Trim().ToLowerInvariant() ?? string.Empty;

        if (PlanningChannelSupport.IsOohFamilyChannel(normalizedMediaType)
            || normalizedSubtype.Contains("billboard", StringComparison.OrdinalIgnoreCase))
        {
            return 0m;
        }

        if (normalizedMediaType == "radio")
        {
            return Math.Max(0m, settings.RadioMarkupPercent);
        }

        if (normalizedMediaType == "tv")
        {
            return Math.Max(0m, settings.TvMarkupPercent);
        }

        if (normalizedMediaType == PlanningChannelSupport.Newspaper)
        {
            return Math.Max(0m, settings.NewspaperMarkupPercent);
        }

        if (normalizedMediaType == "digital")
        {
            return Math.Max(0m, settings.DigitalMarkupPercent);
        }

        return 0m;
    }

    public static decimal ApplyMarkup(decimal baseCost, string mediaType, string? subtype, PricingSettingsSnapshot settings)
    {
        if (baseCost <= 0m)
        {
            return 0m;
        }

        var markupPercent = ResolveMarkupPercent(mediaType, subtype, settings);
        return Math.Round(baseCost * (1m + markupPercent), 2, MidpointRounding.AwayFromZero);
    }

    public static decimal ResolveOohRevenueSharePercent(string mediaType, string? subtype, PricingSettingsSnapshot settings)
    {
        var normalizedMediaType = PlanningChannelSupport.NormalizeChannel(mediaType);
        var normalizedSubtype = subtype?.Trim().ToLowerInvariant() ?? string.Empty;

        return PlanningChannelSupport.IsOohFamilyChannel(normalizedMediaType)
            || normalizedSubtype.Contains("billboard", StringComparison.OrdinalIgnoreCase)
            ? Math.Max(0m, settings.OohMarkupPercent)
            : 0m;
    }

    public static decimal CalculateEmbeddedRevenueShareAmount(decimal grossSellPrice, decimal revenueSharePercent)
    {
        if (grossSellPrice <= 0m)
        {
            return 0m;
        }

        return Math.Round(grossSellPrice * Math.Max(0m, revenueSharePercent), 2, MidpointRounding.AwayFromZero);
    }

    public static decimal CalculateSupplierNetFromEmbeddedRevenueShare(decimal grossSellPrice, decimal revenueSharePercent)
    {
        if (grossSellPrice <= 0m)
        {
            return 0m;
        }

        var revenueShare = CalculateEmbeddedRevenueShareAmount(grossSellPrice, revenueSharePercent);
        return Math.Round(Math.Max(0m, grossSellPrice - revenueShare), 2, MidpointRounding.AwayFromZero);
    }

    public static SalesCommissionBreakdown CalculateSalesCommission(decimal grossTransactionValue, PricingSettingsSnapshot settings)
    {
        if (grossTransactionValue <= 0m)
        {
            return new SalesCommissionBreakdown(0m, 0m, 0m, 0m, "none");
        }

        var commissionPercent = Math.Max(0m, settings.SalesCommissionPercent);
        var pool = Math.Round(grossTransactionValue * commissionPercent, 2, MidpointRounding.AwayFromZero);
        var isAtOrAboveThreshold = grossTransactionValue >= Math.Max(0m, settings.SalesCommissionThresholdZar);
        var agentSharePercent = Math.Max(
            0m,
            isAtOrAboveThreshold
                ? settings.SalesAgentShareAtOrAboveThresholdPercent
                : settings.SalesAgentShareBelowThresholdPercent);
        var salesAgentAmount = Math.Round(pool * agentSharePercent, 2, MidpointRounding.AwayFromZero);
        var advertifiedSalesAmount = Math.Round(Math.Max(0m, pool - salesAgentAmount), 2, MidpointRounding.AwayFromZero);

        return new SalesCommissionBreakdown(
            commissionPercent,
            pool,
            salesAgentAmount,
            advertifiedSalesAmount,
            isAtOrAboveThreshold ? "at_or_above_threshold" : "below_threshold",
            agentSharePercent);
    }
}

public sealed record SalesCommissionBreakdown(
    decimal CommissionPercent,
    decimal PoolAmount,
    decimal SalesAgentAmount,
    decimal AdvertifiedSalesAmount,
    string Tier,
    decimal SalesAgentSharePercent = 0m);
