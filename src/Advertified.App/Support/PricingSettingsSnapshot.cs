namespace Advertified.App.Support;

public sealed record PricingSettingsSnapshot(
    decimal AiStudioReservePercent,
    decimal OohMarkupPercent,
    decimal RadioMarkupPercent,
    decimal TvMarkupPercent,
    decimal NewspaperMarkupPercent,
    decimal DigitalMarkupPercent,
    decimal SalesCommissionPercent,
    decimal SalesCommissionThresholdZar,
    decimal SalesAgentShareBelowThresholdPercent,
    decimal SalesAgentShareAtOrAboveThresholdPercent);
