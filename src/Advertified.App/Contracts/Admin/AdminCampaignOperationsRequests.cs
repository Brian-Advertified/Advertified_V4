namespace Advertified.App.Contracts.Admin;

public sealed class AdminProcessRefundRequest
{
    public decimal? Amount { get; set; }
    public decimal? GatewayFeeRetainedAmount { get; set; }
    public string? Reason { get; set; }
}

public sealed class AdminPauseCampaignRequest
{
    public string? Reason { get; set; }
}

public sealed class AdminUnpauseCampaignRequest
{
    public string? Reason { get; set; }
}
