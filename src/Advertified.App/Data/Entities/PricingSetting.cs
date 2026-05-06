namespace Advertified.App.Data.Entities;

public sealed class PricingSetting
{
    public string PricingKey { get; set; } = "default";
    public decimal AiStudioReservePercent { get; set; }
    public decimal OohMarkupPercent { get; set; }
    public decimal RadioMarkupPercent { get; set; }
    public decimal TvMarkupPercent { get; set; }
    public decimal NewspaperMarkupPercent { get; set; }
    public decimal DigitalMarkupPercent { get; set; }
    public decimal SalesCommissionPercent { get; set; }
    public decimal SalesCommissionThresholdZar { get; set; }
    public decimal SalesAgentShareBelowThresholdPercent { get; set; }
    public decimal SalesAgentShareAtOrAboveThresholdPercent { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
