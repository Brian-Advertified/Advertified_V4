namespace Advertified.App.Contracts.Admin;

public sealed class UpdateAdminPricingSettingsRequest
{
    public decimal AiStudioReservePercent { get; set; }
    public decimal OohMarkupPercent { get; set; }
    public decimal RadioMarkupPercent { get; set; }
    public decimal TvMarkupPercent { get; set; }
    public decimal DigitalMarkupPercent { get; set; }
}
