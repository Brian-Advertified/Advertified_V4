namespace Advertified.App.Contracts.Agent;

public sealed class UpdateProspectPricingRequest
{
    public Guid PackageBandId { get; set; }
    public decimal SelectedBudget { get; set; }
}
