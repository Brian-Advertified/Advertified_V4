namespace Advertified.App.Configuration;

public sealed class LeadScoringOptions
{
    public const string SectionName = "LeadScoring";

    public int BaseScore { get; set; }

    public LeadSignalScoringWeights Weights { get; set; } = new();

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
