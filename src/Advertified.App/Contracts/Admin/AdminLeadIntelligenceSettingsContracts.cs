namespace Advertified.App.Contracts.Admin;

public sealed class AdminLeadIntelligenceSettingsResponse
{
    public AdminLeadScoringSettingsResponse Scoring { get; set; } = new();

    public AdminLeadIntelligenceAutomationSettingsResponse Automation { get; set; } = new();
}

public sealed class AdminLeadScoringSettingsResponse
{
    public int BaseScore { get; set; }

    public AdminLeadActivityScoringSettingsResponse ActivityWeights { get; set; } = new();

    public AdminLeadOpportunityScoringSettingsResponse OpportunityWeights { get; set; } = new();

    public AdminLeadScoringSignalThresholdsResponse SignalThresholds { get; set; } = new();

    public AdminLeadIntentThresholdsResponse Thresholds { get; set; } = new();
}

public sealed class AdminLeadActivityScoringSettingsResponse
{
    public int PromoActive { get; set; }

    public int MetaStrong { get; set; }

    public int WebsiteActive { get; set; }

    public int MultiChannelPresence { get; set; }
}

public sealed class AdminLeadOpportunityScoringSettingsResponse
{
    public int DigitalStrongButSearchWeak { get; set; }

    public int DigitalStrongButOohWeak { get; set; }

    public int PromoHeavyButBrandPresenceWeak { get; set; }

    public int SingleChannelDependency { get; set; }
}

public sealed class AdminLeadScoringSignalThresholdsResponse
{
    public int StrongChannelMin { get; set; }

    public int WeakChannelMax { get; set; }

    public int ActiveChannelMin { get; set; }
}

public sealed class AdminLeadIntentThresholdsResponse
{
    public int LowMax { get; set; }

    public int MediumMax { get; set; }
}

public sealed class UpdateAdminLeadScoringSettingsRequest
{
    public int BaseScore { get; set; }

    public AdminLeadActivityScoringSettingsResponse ActivityWeights { get; set; } = new();

    public AdminLeadOpportunityScoringSettingsResponse OpportunityWeights { get; set; } = new();

    public AdminLeadScoringSignalThresholdsResponse SignalThresholds { get; set; } = new();

    public AdminLeadIntentThresholdsResponse Thresholds { get; set; } = new();
}

public sealed class AdminLeadIntelligenceAutomationSettingsResponse
{
    public bool Enabled { get; set; }

    public int RefreshIntervalMinutes { get; set; }

    public int BatchSize { get; set; }

    public bool RunOnStartup { get; set; }

    public bool EnablePaidMediaEvidenceSync { get; set; }

    public int PaidMediaSyncIntervalMinutes { get; set; }
}

public sealed class UpdateAdminLeadIntelligenceAutomationSettingsRequest
{
    public bool Enabled { get; set; }

    public int RefreshIntervalMinutes { get; set; }

    public int BatchSize { get; set; }

    public bool RunOnStartup { get; set; }

    public bool EnablePaidMediaEvidenceSync { get; set; }

    public int PaidMediaSyncIntervalMinutes { get; set; }
}
