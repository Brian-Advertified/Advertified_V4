namespace Advertified.App.Configuration;

public sealed class PlanningPolicyOptions
{
    public const string SectionName = "PlanningPolicy";

    public PackagePlanningPolicy Scale { get; set; } = new();
    public PackagePlanningPolicy Dominance { get; set; } = new();
}

public sealed class PackagePlanningPolicy
{
    public decimal BudgetFloor { get; set; }
    public int MinimumNationalRadioCandidates { get; set; }
    public bool RequireNationalCapableRadio { get; set; }
    public bool RequirePremiumNationalRadio { get; set; }
    public int NationalRadioBonus { get; set; }
    public int NonNationalRadioPenalty { get; set; }
    public int RegionalRadioPenalty { get; set; }
}
