using System.Text.Json;
using Advertified.App.Domain.Campaigns;
using Dapper;
using Npgsql;

namespace Advertified.App.Services;

public sealed class PlanningBudgetAllocationSnapshotProvider
{
    private const string ChannelRulesKey = "allocation_channel_rules_json";
    private const string GeoRulesKey = "allocation_geo_rules_json";

    private readonly NpgsqlDataSource? _dataSource;
    private readonly PlanningBudgetAllocationPolicySnapshot _fallback;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public PlanningBudgetAllocationSnapshotProvider(NpgsqlDataSource dataSource)
        : this(dataSource, new PlanningBudgetAllocationPolicySnapshot())
    {
    }

    public PlanningBudgetAllocationSnapshotProvider(PlanningBudgetAllocationPolicySnapshot fallback)
    {
        _fallback = fallback;
    }

    public PlanningBudgetAllocationSnapshotProvider(NpgsqlDataSource dataSource, PlanningBudgetAllocationPolicySnapshot fallback)
    {
        _dataSource = dataSource;
        _fallback = fallback;
    }

    public PlanningBudgetAllocationPolicySnapshot GetCurrent()
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
                where setting_key in (@ChannelRulesKey, @GeoRulesKey);",
                new { ChannelRulesKey, GeoRulesKey })
            .ToDictionary(row => row.SettingKey, row => row.SettingValue, StringComparer.OrdinalIgnoreCase);

        return new PlanningBudgetAllocationPolicySnapshot
        {
            ChannelRules = Deserialize<List<ChannelAllocationPolicyRule>>(rows, ChannelRulesKey)
                ?? _fallback.ChannelRules,
            GeoRules = Deserialize<List<GeoAllocationPolicyRule>>(rows, GeoRulesKey)
                ?? _fallback.GeoRules
        };
    }

    private static T? Deserialize<T>(IReadOnlyDictionary<string, string> rows, string key)
    {
        if (!rows.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value))
        {
            return default;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(value, JsonOptions);
        }
        catch (JsonException)
        {
            return default;
        }
    }

    private sealed class SettingRecord
    {
        public string SettingKey { get; set; } = string.Empty;
        public string SettingValue { get; set; } = string.Empty;
    }
}
