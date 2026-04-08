namespace Advertified.App.Domain.Campaigns;

public sealed class PlanningPolicyContext
{
    public string PackagePolicyCode { get; set; } = string.Empty;
    public decimal BudgetFloor { get; set; }
    public int MinimumNationalRadioCandidates { get; set; }
    public bool RequireNationalCapableRadio { get; set; }
    public bool RequirePremiumNationalRadio { get; set; }
    public int NationalRadioBonus { get; set; }
    public int NonNationalRadioPenalty { get; set; }
    public int RegionalRadioPenalty { get; set; }
    public string? RequestedMixLabel { get; set; }
    public IReadOnlyList<RequestedChannelShare> RequestedChannelShares { get; set; } = Array.Empty<RequestedChannelShare>();
    public IReadOnlyList<string> RequiredChannels { get; set; } = Array.Empty<string>();
}

public sealed class RequestedChannelShare
{
    public string Channel { get; set; } = string.Empty;
    public int Share { get; set; }
}
