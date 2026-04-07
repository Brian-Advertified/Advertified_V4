namespace Advertified.App.Configuration;

public sealed class LeadIntelligenceAutomationOptions
{
    public const string SectionName = "LeadIntelligenceAutomation";

    public bool Enabled { get; set; }

    public int RefreshIntervalMinutes { get; set; } = 60;

    public int BatchSize { get; set; } = 100;

    public bool RunOnStartup { get; set; }
}
