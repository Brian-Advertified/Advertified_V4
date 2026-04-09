namespace Advertified.App.Services;

public sealed class LeadIndustryPolicyProfile
{
    public string Key { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string? ObjectiveOverride { get; init; }

    public string? PreferredTone { get; init; }

    public IReadOnlyList<string> PreferredChannels { get; init; } = Array.Empty<string>();

    public string Cta { get; init; } = string.Empty;

    public string MessagingAngle { get; init; } = string.Empty;

    public IReadOnlyList<string> Guardrails { get; init; } = Array.Empty<string>();

    public string AdditionalGap { get; init; } = string.Empty;

    public string AdditionalOutcome { get; init; } = string.Empty;
}
