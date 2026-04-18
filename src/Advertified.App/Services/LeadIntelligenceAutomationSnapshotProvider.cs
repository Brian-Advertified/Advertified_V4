using Advertified.App.Configuration;
using Dapper;
using Npgsql;

namespace Advertified.App.Services;

public sealed class LeadIntelligenceAutomationSnapshotProvider
{
    private readonly NpgsqlDataSource? _dataSource;
    private readonly LeadIntelligenceAutomationOptions _fallback;

    public LeadIntelligenceAutomationSnapshotProvider(NpgsqlDataSource dataSource, LeadIntelligenceAutomationOptions fallback)
    {
        _dataSource = dataSource;
        _fallback = Clone(fallback);
    }

    public LeadIntelligenceAutomationSnapshotProvider(LeadIntelligenceAutomationOptions fallback)
    {
        _fallback = Clone(fallback);
    }

    public LeadIntelligenceAutomationOptions GetCurrent()
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
            where setting_key like 'automation_%';")
            .ToDictionary(row => row.SettingKey, row => row.SettingValue, StringComparer.OrdinalIgnoreCase);

        return new LeadIntelligenceAutomationOptions
        {
            Enabled = GetBool(rows, "automation_enabled", _fallback.Enabled),
            RefreshIntervalMinutes = GetInt(rows, "automation_refresh_interval_minutes", _fallback.RefreshIntervalMinutes),
            BatchSize = GetInt(rows, "automation_batch_size", _fallback.BatchSize),
            RunOnStartup = GetBool(rows, "automation_run_on_startup", _fallback.RunOnStartup),
            EnablePaidMediaEvidenceSync = GetBool(rows, "automation_enable_paid_media_evidence_sync", _fallback.EnablePaidMediaEvidenceSync),
            PaidMediaSyncIntervalMinutes = GetInt(rows, "automation_paid_media_sync_interval_minutes", _fallback.PaidMediaSyncIntervalMinutes)
        };
    }

    private static int GetInt(IReadOnlyDictionary<string, string> rows, string key, int fallback)
        => rows.TryGetValue(key, out var value) && int.TryParse(value, out var parsed) ? parsed : fallback;

    private static bool GetBool(IReadOnlyDictionary<string, string> rows, string key, bool fallback)
        => rows.TryGetValue(key, out var value) && bool.TryParse(value, out var parsed) ? parsed : fallback;

    private static LeadIntelligenceAutomationOptions Clone(LeadIntelligenceAutomationOptions source)
    {
        return new LeadIntelligenceAutomationOptions
        {
            Enabled = source.Enabled,
            RefreshIntervalMinutes = source.RefreshIntervalMinutes,
            BatchSize = source.BatchSize,
            RunOnStartup = source.RunOnStartup,
            EnablePaidMediaEvidenceSync = source.EnablePaidMediaEvidenceSync,
            PaidMediaSyncIntervalMinutes = source.PaidMediaSyncIntervalMinutes
        };
    }

    private sealed class SettingRecord
    {
        public string SettingKey { get; set; } = string.Empty;
        public string SettingValue { get; set; } = string.Empty;
    }
}
