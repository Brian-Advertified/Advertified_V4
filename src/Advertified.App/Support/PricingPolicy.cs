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
        var normalizedMediaType = mediaType.Trim().ToLowerInvariant();
        var normalizedSubtype = subtype?.Trim().ToLowerInvariant() ?? string.Empty;

        if (normalizedMediaType == "ooh" || normalizedSubtype.Contains("billboard", StringComparison.OrdinalIgnoreCase))
        {
            return Math.Max(0m, settings.OohMarkupPercent);
        }

        if (normalizedMediaType == "radio")
        {
            return Math.Max(0m, settings.RadioMarkupPercent);
        }

        if (normalizedMediaType == "tv")
        {
            return Math.Max(0m, settings.TvMarkupPercent);
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
}
