namespace Advertified.App.Contracts.Admin;

public sealed class CreateAdminLeadIndustryPolicyRequest
{
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? ObjectiveOverride { get; set; }
    public string? PreferredTone { get; set; }
    public IReadOnlyList<string> PreferredChannels { get; set; } = Array.Empty<string>();
    public string Cta { get; set; } = string.Empty;
    public string MessagingAngle { get; set; } = string.Empty;
    public IReadOnlyList<string> Guardrails { get; set; } = Array.Empty<string>();
    public string AdditionalGap { get; set; } = string.Empty;
    public string AdditionalOutcome { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class UpdateAdminLeadIndustryPolicyRequest
{
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? ObjectiveOverride { get; set; }
    public string? PreferredTone { get; set; }
    public IReadOnlyList<string> PreferredChannels { get; set; } = Array.Empty<string>();
    public string Cta { get; set; } = string.Empty;
    public string MessagingAngle { get; set; } = string.Empty;
    public IReadOnlyList<string> Guardrails { get; set; } = Array.Empty<string>();
    public string AdditionalGap { get; set; } = string.Empty;
    public string AdditionalOutcome { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}
