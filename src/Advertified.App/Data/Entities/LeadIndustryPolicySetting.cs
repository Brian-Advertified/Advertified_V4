namespace Advertified.App.Data.Entities;

public sealed class LeadIndustryPolicySetting
{
    public string Key { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? ObjectiveOverride { get; set; }

    public string? PreferredTone { get; set; }

    public string PreferredChannelsJson { get; set; } = "[]";

    public string Cta { get; set; } = string.Empty;

    public string MessagingAngle { get; set; } = string.Empty;

    public string GuardrailsJson { get; set; } = "[]";

    public string AdditionalGap { get; set; } = string.Empty;

    public string AdditionalOutcome { get; set; } = string.Empty;

    public int SortOrder { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
