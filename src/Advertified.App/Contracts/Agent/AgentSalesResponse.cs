namespace Advertified.App.Contracts.Agent;

public sealed class AgentSalesResponse
{
    public int TotalSalesCount { get; set; }
    public int ConvertedProspectSalesCount { get; set; }
    public decimal TotalChargedAmount { get; set; }
    public decimal TotalSelectedBudget { get; set; }
    public IReadOnlyList<AgentSaleItemResponse> Items { get; set; } = Array.Empty<AgentSaleItemResponse>();
}

public sealed class AgentSaleItemResponse
{
    public Guid CampaignId { get; set; }
    public Guid PackageOrderId { get; set; }
    public string CampaignName { get; set; } = string.Empty;
    public string ClientName { get; set; } = string.Empty;
    public string ClientEmail { get; set; } = string.Empty;
    public string PackageBandName { get; set; } = string.Empty;
    public decimal SelectedBudget { get; set; }
    public decimal ChargedAmount { get; set; }
    public string PaymentProvider { get; set; } = string.Empty;
    public string? PaymentReference { get; set; }
    public bool ConvertedFromProspect { get; set; }
    public DateTimeOffset PurchasedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
