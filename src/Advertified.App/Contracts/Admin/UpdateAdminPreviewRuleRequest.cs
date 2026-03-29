namespace Advertified.App.Contracts.Admin;

public sealed class UpdateAdminPreviewRuleRequest
{
    public string TierLabel { get; set; } = string.Empty;
    public IReadOnlyList<string> TypicalInclusions { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> IndicativeMix { get; set; } = Array.Empty<string>();
}
