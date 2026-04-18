namespace Advertified.App.Data.Entities;

public sealed class LeadIntelligenceSetting
{
    public string SettingKey { get; set; } = string.Empty;

    public string SettingValue { get; set; } = string.Empty;

    public string? Description { get; set; }

    public DateTime UpdatedAt { get; set; }
}
