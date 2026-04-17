using Advertified.App.Domain.Campaigns;
using Dapper;
using Npgsql;

namespace Advertified.App.Services;

public sealed class PlanningBriefIntentSettingsSnapshotProvider
{
    private readonly NpgsqlDataSource? _dataSource;
    private readonly PlanningBriefIntentSettingsSnapshot _fallback;

    public PlanningBriefIntentSettingsSnapshotProvider(NpgsqlDataSource dataSource)
        : this(dataSource, new PlanningBriefIntentSettingsSnapshot())
    {
    }

    public PlanningBriefIntentSettingsSnapshotProvider(PlanningBriefIntentSettingsSnapshot fallback)
    {
        _fallback = fallback;
    }

    public PlanningBriefIntentSettingsSnapshotProvider(NpgsqlDataSource dataSource, PlanningBriefIntentSettingsSnapshot fallback)
    {
        _dataSource = dataSource;
        _fallback = fallback;
    }

    public PlanningBriefIntentSettingsSnapshot GetCurrent()
    {
        if (_dataSource is null)
        {
            return _fallback;
        }

        using var connection = _dataSource.OpenConnection();
        var rows = connection.Query<SettingRecord>(
            @"
            select
                setting_key as SettingKey,
                setting_value as SettingValue
            from planning_engine_settings
            where setting_key like 'brief_intent_%';")
            .ToDictionary(row => row.SettingKey, row => row.SettingValue, StringComparer.OrdinalIgnoreCase);

        return new PlanningBriefIntentSettingsSnapshot
        {
            LocalOohMinDimensionMatches = GetInt(rows, "brief_intent_local_ooh_min_dimension_matches", _fallback.LocalOohMinDimensionMatches),
            LocalOohRadiusKm = GetDouble(rows, "brief_intent_local_ooh_radius_km", _fallback.LocalOohRadiusKm),
            RelaxedLocalOohRadiusKm = GetDouble(rows, "brief_intent_relaxed_local_ooh_radius_km", _fallback.RelaxedLocalOohRadiusKm),
            ScorePerMatch = GetDecimal(rows, "brief_intent_score_per_match", _fallback.ScorePerMatch),
            FullMatchBonus = GetDecimal(rows, "brief_intent_full_match_bonus", _fallback.FullMatchBonus),
            RequireLocalOohAudienceEvidence = GetBool(rows, "brief_intent_require_local_ooh_audience_evidence", _fallback.RequireLocalOohAudienceEvidence)
        };
    }

    private static int GetInt(IReadOnlyDictionary<string, string> rows, string key, int fallback)
        => rows.TryGetValue(key, out var value) && int.TryParse(value, out var parsed) ? parsed : fallback;

    private static double GetDouble(IReadOnlyDictionary<string, string> rows, string key, double fallback)
        => rows.TryGetValue(key, out var value) && double.TryParse(value, out var parsed) ? parsed : fallback;

    private static decimal GetDecimal(IReadOnlyDictionary<string, string> rows, string key, decimal fallback)
        => rows.TryGetValue(key, out var value) && decimal.TryParse(value, out var parsed) ? parsed : fallback;

    private static bool GetBool(IReadOnlyDictionary<string, string> rows, string key, bool fallback)
        => rows.TryGetValue(key, out var value) && bool.TryParse(value, out var parsed) ? parsed : fallback;

    private sealed class SettingRecord
    {
        public string SettingKey { get; set; } = string.Empty;
        public string SettingValue { get; set; } = string.Empty;
    }
}
