namespace Advertified.App.Configuration;

public sealed class LeadScoringOptions
{
    public const string SectionName = "LeadScoring";

    public int BaseScore { get; set; }

    public LeadSignalScoringWeights Weights { get; set; } = new();

    public LeadActivityScoringWeights ActivityWeights { get; set; } = new();

    public LeadOpportunityScoringWeights OpportunityWeights { get; set; } = new();

    public LeadScoringSignalThresholds SignalThresholds { get; set; } = new();

    public LeadIntentThresholds Thresholds { get; set; } = new();
}

public sealed class LeadSignalScoringWeights
{
    public int HasPromo { get; set; }

    public int HasMetaAds { get; set; }

    public int WebsiteUpdatedRecently { get; set; }
}

public sealed class LeadIntentThresholds
{
    public int LowMax { get; set; }

    public int MediumMax { get; set; }
}

public sealed class LeadActivityScoringWeights
{
    public int PromoActive { get; set; } = 15;

    public int MetaStrong { get; set; } = 20;

    public int WebsiteActive { get; set; } = 10;

    public int MultiChannelPresence { get; set; } = 5;
}

public sealed class LeadOpportunityScoringWeights
{
    public int DigitalStrongButSearchWeak { get; set; } = 15;

    public int DigitalStrongButOohWeak { get; set; } = 15;

    public int PromoHeavyButBrandPresenceWeak { get; set; } = 10;

    public int SingleChannelDependency { get; set; } = 10;
}

public sealed class LeadScoringSignalThresholds
{
    public int StrongChannelMin { get; set; } = 60;

    public int WeakChannelMax { get; set; } = 39;

    public int ActiveChannelMin { get; set; } = 40;
}
