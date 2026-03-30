namespace Advertified.App.Contracts.Packages;

public sealed class PackagePricingSummaryResponse
{
    public decimal SelectedBudget { get; set; }
    public decimal ChargedAmount { get; set; }
    public decimal AiStudioReserveAmount { get; set; }
}
