using System.Text.Json;
using Advertified.App.Services.Abstractions;
using Dapper;
using Npgsql;

namespace Advertified.App.Services;

public sealed class IndustryStrategyCatalogService : IIndustryStrategyCatalogService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly NpgsqlDataSource _dataSource;
    private readonly object _syncRoot = new();
    private IReadOnlyDictionary<string, IndustryStrategyCatalogProfile>? _profilesByCode;

    public IndustryStrategyCatalogService(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public IndustryStrategyCatalogProfile? Resolve(string? industryCode)
    {
        var normalized = Normalize(industryCode);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        var profiles = GetProfiles();
        return profiles.TryGetValue(normalized, out var profile)
            ? profile
            : null;
    }

    public IReadOnlyCollection<string> GetSupportedIndustryCodes()
    {
        return GetProfiles().Keys.ToArray();
    }

    private IReadOnlyDictionary<string, IndustryStrategyCatalogProfile> GetProfiles()
    {
        if (_profilesByCode is not null)
        {
            return _profilesByCode;
        }

        lock (_syncRoot)
        {
            _profilesByCode ??= LoadProfiles();
            return _profilesByCode;
        }
    }

    private IReadOnlyDictionary<string, IndustryStrategyCatalogProfile> LoadProfiles()
    {
        using var connection = _dataSource.OpenConnection();
        var rows = connection.Query<IndustryStrategyCatalogRow>(
            @"select
                mi.code as IndustryCode,
                mi.label as IndustryLabel,
                profile.primary_persona as PrimaryPersona,
                profile.buying_journey as BuyingJourney,
                profile.trust_sensitivity as TrustSensitivity,
                profile.default_language_biases_json as DefaultLanguageBiasesJson,
                profile.default_objective as DefaultObjective,
                profile.funnel_shape as FunnelShape,
                profile.primary_kpis_json as PrimaryKpisJson,
                profile.sales_cycle as SalesCycle,
                profile.preferred_channels_json as PreferredChannelsJson,
                profile.base_budget_split_json as BaseBudgetSplitJson,
                profile.geography_bias as GeographyBias,
                profile.preferred_tone as PreferredTone,
                profile.messaging_angle as MessagingAngle,
                profile.recommended_cta as RecommendedCta,
                profile.proof_points_json as ProofPointsJson,
                profile.guardrails_json as GuardrailsJson,
                profile.restricted_claim_types_json as RestrictedClaimTypesJson,
                profile.research_summary as ResearchSummary,
                profile.research_sources_json as ResearchSourcesJson
            from master_industry_strategy_profiles profile
            join master_industries mi on mi.id = profile.master_industry_id;");

        return rows
            .Where(row => !string.IsNullOrWhiteSpace(row.IndustryCode))
            .GroupBy(row => Normalize(row.IndustryCode))
            .ToDictionary(
                group => group.Key,
                group =>
                {
                    var row = group.First();
                    return new IndustryStrategyCatalogProfile
                    {
                        IndustryCode = row.IndustryCode.Trim(),
                        IndustryLabel = row.IndustryLabel.Trim(),
                        Audience = new IndustryAudienceProfile
                        {
                            PrimaryPersona = row.PrimaryPersona ?? string.Empty,
                            BuyingJourney = row.BuyingJourney ?? string.Empty,
                            TrustSensitivity = row.TrustSensitivity ?? string.Empty,
                            DefaultLanguageBiases = DeserializeList(row.DefaultLanguageBiasesJson),
                            AudienceHints = Array.Empty<string>()
                        },
                        Campaign = new IndustryCampaignProfile
                        {
                            DefaultObjective = row.DefaultObjective ?? string.Empty,
                            FunnelShape = row.FunnelShape ?? string.Empty,
                            PrimaryKpis = DeserializeList(row.PrimaryKpisJson),
                            SalesCycle = row.SalesCycle ?? string.Empty
                        },
                        Channels = new IndustryChannelProfile
                        {
                            PreferredChannels = DeserializeList(row.PreferredChannelsJson),
                            BaseBudgetSplit = DeserializeDictionary(row.BaseBudgetSplitJson),
                            GeographyBias = row.GeographyBias ?? string.Empty
                        },
                        Creative = new IndustryCreativeProfile
                        {
                            PreferredTone = row.PreferredTone ?? string.Empty,
                            MessagingAngle = row.MessagingAngle ?? string.Empty,
                            RecommendedCta = row.RecommendedCta ?? string.Empty,
                            ProofPoints = DeserializeList(row.ProofPointsJson)
                        },
                        Compliance = new IndustryComplianceProfile
                        {
                            Guardrails = DeserializeList(row.GuardrailsJson),
                            RestrictedClaimTypes = DeserializeList(row.RestrictedClaimTypesJson)
                        },
                        Research = new IndustryResearchProfile
                        {
                            Summary = row.ResearchSummary ?? string.Empty,
                            Sources = DeserializeList(row.ResearchSourcesJson)
                        }
                    };
                },
                StringComparer.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<string> DeserializeList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Array.Empty<string>();
        }

        return JsonSerializer.Deserialize<string[]>(json, SerializerOptions)
            ?.Where(static item => !string.IsNullOrWhiteSpace(item))
            .Select(static item => item.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray()
            ?? Array.Empty<string>();
    }

    private static IReadOnlyDictionary<string, int> DeserializeDictionary(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        }

        var parsed = JsonSerializer.Deserialize<Dictionary<string, int>>(json, SerializerOptions)
            ?? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        return new Dictionary<string, int>(
            parsed
                .Where(static entry => !string.IsNullOrWhiteSpace(entry.Key))
                .ToDictionary(entry => entry.Key.Trim(), entry => entry.Value, StringComparer.OrdinalIgnoreCase),
            StringComparer.OrdinalIgnoreCase);
    }

    private static string Normalize(string? value)
    {
        return (value ?? string.Empty).Trim().ToLowerInvariant();
    }

    private sealed class IndustryStrategyCatalogRow
    {
        public string IndustryCode { get; init; } = string.Empty;
        public string IndustryLabel { get; init; } = string.Empty;
        public string? PrimaryPersona { get; init; }
        public string? BuyingJourney { get; init; }
        public string? TrustSensitivity { get; init; }
        public string? DefaultLanguageBiasesJson { get; init; }
        public string? DefaultObjective { get; init; }
        public string? FunnelShape { get; init; }
        public string? PrimaryKpisJson { get; init; }
        public string? SalesCycle { get; init; }
        public string? PreferredChannelsJson { get; init; }
        public string? BaseBudgetSplitJson { get; init; }
        public string? GeographyBias { get; init; }
        public string? PreferredTone { get; init; }
        public string? MessagingAngle { get; init; }
        public string? RecommendedCta { get; init; }
        public string? ProofPointsJson { get; init; }
        public string? GuardrailsJson { get; init; }
        public string? RestrictedClaimTypesJson { get; init; }
        public string? ResearchSummary { get; init; }
        public string? ResearchSourcesJson { get; init; }
    }
}
