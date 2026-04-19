namespace Advertified.App.Contracts.Admin;

public sealed class AdminPlanningAllocationSettingsResponse
{
    public IReadOnlyList<AdminPlanningBudgetBandResponse> BudgetBands { get; set; } = Array.Empty<AdminPlanningBudgetBandResponse>();
    public AdminPlanningAllocationGlobalRulesResponse GlobalRules { get; set; } = new();
}

public sealed class AdminPlanningBudgetBandResponse
{
    public string Name { get; set; } = string.Empty;
    public decimal Min { get; set; }
    public decimal Max { get; set; }
    public decimal OohTarget { get; set; }
    public decimal TvMin { get; set; }
    public bool TvEligible { get; set; }
    public decimal[] RadioRange { get; set; } = Array.Empty<decimal>();
    public decimal[] DigitalRange { get; set; } = Array.Empty<decimal>();
}

public sealed class AdminPlanningAllocationGlobalRulesResponse
{
    public decimal MaxOoh { get; set; }
    public decimal MinDigital { get; set; }
    public bool EnforceTvFloorIfPreferred { get; set; }
}

public sealed class UpdateAdminPlanningAllocationSettingsRequest
{
    public IReadOnlyList<AdminPlanningBudgetBandInput> BudgetBands { get; set; } = Array.Empty<AdminPlanningBudgetBandInput>();
    public AdminPlanningAllocationGlobalRulesInput GlobalRules { get; set; } = new();
}

public sealed class AdminPlanningBudgetBandInput
{
    public string Name { get; set; } = string.Empty;
    public decimal Min { get; set; }
    public decimal Max { get; set; }
    public decimal OohTarget { get; set; }
    public decimal TvMin { get; set; }
    public bool TvEligible { get; set; }
    public decimal[] RadioRange { get; set; } = Array.Empty<decimal>();
    public decimal[] DigitalRange { get; set; } = Array.Empty<decimal>();
}

public sealed class AdminPlanningAllocationGlobalRulesInput
{
    public decimal MaxOoh { get; set; }
    public decimal MinDigital { get; set; }
    public bool EnforceTvFloorIfPreferred { get; set; }
}
