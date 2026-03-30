namespace Advertified.App.Contracts.Admin;

public sealed class UpdateAdminEnginePolicyRequest
{
    public decimal BudgetFloor { get; set; }
    public int MinimumNationalRadioCandidates { get; set; }
    public bool RequireNationalCapableRadio { get; set; }
    public bool RequirePremiumNationalRadio { get; set; }
    public int NationalRadioBonus { get; set; }
    public int NonNationalRadioPenalty { get; set; }
    public int RegionalRadioPenalty { get; set; }
}
