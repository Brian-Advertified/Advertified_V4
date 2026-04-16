using System.Text.Json;
using Advertified.App.Configuration;
using Dapper;
using Npgsql;

namespace Advertified.App.Services;

public sealed class LeadIndustryPolicySnapshotProvider
{
    private readonly NpgsqlDataSource? _dataSource;
    private readonly LeadIndustryPolicyOptions _fallbackOptions;

    public LeadIndustryPolicySnapshotProvider(NpgsqlDataSource dataSource, LeadIndustryPolicyOptions fallbackOptions)
    {
        _dataSource = dataSource;
        _fallbackOptions = fallbackOptions;
    }

    public LeadIndustryPolicySnapshotProvider(LeadIndustryPolicyOptions fallbackOptions)
    {
        _fallbackOptions = fallbackOptions;
    }

    public IReadOnlyList<LeadIndustryPolicyProfile> GetCurrent()
    {
        if (_dataSource is null)
        {
            return GetFallbackProfiles();
        }

        using var connection = _dataSource.OpenConnection();
        var rows = connection.Query<LeadIndustryPolicyRecord>(
            @"
            select
                key as Key,
                name as Name,
                objective_override as ObjectiveOverride,
                preferred_tone as PreferredTone,
                preferred_channels_json::text as PreferredChannelsJson,
                cta as Cta,
                messaging_angle as MessagingAngle,
                guardrails_json::text as GuardrailsJson,
                additional_gap as AdditionalGap,
                additional_outcome as AdditionalOutcome
            from lead_industry_policies
            where is_active = true
            order by sort_order, key;");

        var profiles = rows
            .Select(MapRecord)
            .Where(static profile => !string.IsNullOrWhiteSpace(profile.Key))
            .ToArray();

        return profiles.Length > 0 ? profiles : GetFallbackProfiles();
    }

    private IReadOnlyList<LeadIndustryPolicyProfile> GetFallbackProfiles()
    {
        var configuredProfiles = _fallbackOptions.Profiles;
        if (configuredProfiles.Count == 0)
        {
            configuredProfiles = LeadIndustryPolicyOptions.BuildDefaults();
        }

        return configuredProfiles.Select(MapOptions).ToArray();
    }

    private static LeadIndustryPolicyProfile MapOptions(LeadIndustryPolicyProfileOptions source)
    {
        return new LeadIndustryPolicyProfile
        {
            Key = source.Key,
            Name = source.Name,
            ObjectiveOverride = source.ObjectiveOverride,
            PreferredTone = source.PreferredTone,
            PreferredChannels = NormalizeValues(source.PreferredChannels),
            Cta = source.Cta,
            MessagingAngle = source.MessagingAngle,
            Guardrails = NormalizeValues(source.Guardrails),
            AdditionalGap = source.AdditionalGap,
            AdditionalOutcome = source.AdditionalOutcome,
        };
    }

    private static LeadIndustryPolicyProfile MapRecord(LeadIndustryPolicyRecord source)
    {
        return new LeadIndustryPolicyProfile
        {
            Key = source.Key,
            Name = source.Name,
            ObjectiveOverride = source.ObjectiveOverride,
            PreferredTone = source.PreferredTone,
            PreferredChannels = DeserializeJsonList(source.PreferredChannelsJson),
            Cta = source.Cta,
            MessagingAngle = source.MessagingAngle,
            Guardrails = DeserializeJsonList(source.GuardrailsJson),
            AdditionalGap = source.AdditionalGap,
            AdditionalOutcome = source.AdditionalOutcome,
        };
    }

    private static IReadOnlyList<string> DeserializeJsonList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Array.Empty<string>();
        }

        var values = JsonSerializer.Deserialize<List<string>>(json);
        return NormalizeValues(values ?? new List<string>());
    }

    private static IReadOnlyList<string> NormalizeValues(IEnumerable<string> values)
    {
        return values
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private sealed class LeadIndustryPolicyRecord
    {
        public string Key { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? ObjectiveOverride { get; set; }
        public string? PreferredTone { get; set; }
        public string? PreferredChannelsJson { get; set; }
        public string Cta { get; set; } = string.Empty;
        public string MessagingAngle { get; set; } = string.Empty;
        public string? GuardrailsJson { get; set; }
        public string AdditionalGap { get; set; } = string.Empty;
        public string AdditionalOutcome { get; set; } = string.Empty;
    }
}
