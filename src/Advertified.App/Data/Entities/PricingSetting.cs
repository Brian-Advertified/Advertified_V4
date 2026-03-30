namespace Advertified.App.Data.Entities;

public sealed class PricingSetting
{
    public string PricingKey { get; set; } = "default";
    public decimal AiStudioReservePercent { get; set; }
    public decimal OohMarkupPercent { get; set; }
    public decimal RadioMarkupPercent { get; set; }
    public decimal TvMarkupPercent { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
