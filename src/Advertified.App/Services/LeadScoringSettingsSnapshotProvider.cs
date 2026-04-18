using Advertified.App.Configuration;
using Dapper;
using Npgsql;

namespace Advertified.App.Services;

public sealed class LeadScoringSettingsSnapshotProvider
{
    private readonly NpgsqlDataSource? _dataSource;
    private readonly LeadScoringOptions _fallback;

    public LeadScoringSettingsSnapshotProvider(NpgsqlDataSource dataSource, LeadScoringOptions fallback)
    {
        _dataSource = dataSource;
        _fallback = Clone(fallback);
    }

    public LeadScoringSettingsSnapshotProvider(LeadScoringOptions fallback)
    {
        _fallback = Clone(fallback);
    }

    public LeadScoringOptions GetCurrent()
    {
        if (_dataSource is null)
        {
            return Clone(_fallback);
        }

        using var connection = _dataSource.OpenConnection();
        var rows = connection.Query<SettingRecord>(
            @"
            select
                setting_key as SettingKey,
                setting_value as SettingValue
            from lead_intelligence_settings
            where setting_key like 'scoring_%';")
            .ToDictionary(row => row.SettingKey, row => row.SettingValue, StringComparer.OrdinalIgnoreCase);

        return new LeadScoringOptions
        {
            BaseScore = GetInt(rows, "scoring_base_score", _fallback.BaseScore),
            ActivityWeights = new LeadActivityScoringWeights
            {
                PromoActive = GetInt(rows, "scoring_activity_promo_active", _fallback.ActivityWeights.PromoActive),
                MetaStrong = GetInt(rows, "scoring_activity_meta_strong", _fallback.ActivityWeights.MetaStrong),
                WebsiteActive = GetInt(rows, "scoring_activity_website_active", _fallback.ActivityWeights.WebsiteActive),
                MultiChannelPresence = GetInt(rows, "scoring_activity_multi_channel_presence", _fallback.ActivityWeights.MultiChannelPresence)
            },
            OpportunityWeights = new LeadOpportunityScoringWeights
            {
                DigitalStrongButSearchWeak = GetInt(rows, "scoring_opportunity_digital_strong_but_search_weak", _fallback.OpportunityWeights.DigitalStrongButSearchWeak),
                DigitalStrongButOohWeak = GetInt(rows, "scoring_opportunity_digital_strong_but_ooh_weak", _fallback.OpportunityWeights.DigitalStrongButOohWeak),
                PromoHeavyButBrandPresenceWeak = GetInt(rows, "scoring_opportunity_promo_heavy_but_brand_presence_weak", _fallback.OpportunityWeights.PromoHeavyButBrandPresenceWeak),
                SingleChannelDependency = GetInt(rows, "scoring_opportunity_single_channel_dependency", _fallback.OpportunityWeights.SingleChannelDependency)
            },
            SignalThresholds = new LeadScoringSignalThresholds
            {
                StrongChannelMin = GetInt(rows, "scoring_threshold_strong_channel_min", _fallback.SignalThresholds.StrongChannelMin),
                WeakChannelMax = GetInt(rows, "scoring_threshold_weak_channel_max", _fallback.SignalThresholds.WeakChannelMax),
                ActiveChannelMin = GetInt(rows, "scoring_threshold_active_channel_min", _fallback.SignalThresholds.ActiveChannelMin)
            },
            Thresholds = new LeadIntentThresholds
            {
                LowMax = GetInt(rows, "scoring_intent_low_max", _fallback.Thresholds.LowMax),
                MediumMax = GetInt(rows, "scoring_intent_medium_max", _fallback.Thresholds.MediumMax)
            }
        };
    }

    private static int GetInt(IReadOnlyDictionary<string, string> rows, string key, int fallback)
        => rows.TryGetValue(key, out var value) && int.TryParse(value, out var parsed) ? parsed : fallback;

    private static LeadScoringOptions Clone(LeadScoringOptions source)
    {
        return new LeadScoringOptions
        {
            BaseScore = source.BaseScore,
            Weights = new LeadSignalScoringWeights
            {
                HasPromo = source.Weights.HasPromo,
                HasMetaAds = source.Weights.HasMetaAds,
                WebsiteUpdatedRecently = source.Weights.WebsiteUpdatedRecently
            },
            ActivityWeights = new LeadActivityScoringWeights
            {
                PromoActive = source.ActivityWeights.PromoActive,
                MetaStrong = source.ActivityWeights.MetaStrong,
                WebsiteActive = source.ActivityWeights.WebsiteActive,
                MultiChannelPresence = source.ActivityWeights.MultiChannelPresence
            },
            OpportunityWeights = new LeadOpportunityScoringWeights
            {
                DigitalStrongButSearchWeak = source.OpportunityWeights.DigitalStrongButSearchWeak,
                DigitalStrongButOohWeak = source.OpportunityWeights.DigitalStrongButOohWeak,
                PromoHeavyButBrandPresenceWeak = source.OpportunityWeights.PromoHeavyButBrandPresenceWeak,
                SingleChannelDependency = source.OpportunityWeights.SingleChannelDependency
            },
            SignalThresholds = new LeadScoringSignalThresholds
            {
                StrongChannelMin = source.SignalThresholds.StrongChannelMin,
                WeakChannelMax = source.SignalThresholds.WeakChannelMax,
                ActiveChannelMin = source.SignalThresholds.ActiveChannelMin
            },
            Thresholds = new LeadIntentThresholds
            {
                LowMax = source.Thresholds.LowMax,
                MediumMax = source.Thresholds.MediumMax
            }
        };
    }

    private sealed class SettingRecord
    {
        public string SettingKey { get; set; } = string.Empty;
        public string SettingValue { get; set; } = string.Empty;
    }
}
