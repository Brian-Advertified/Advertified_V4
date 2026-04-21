using System.Text.Json;
using Advertified.App.Contracts.Admin;
using Advertified.App.Services.Abstractions;
using Dapper;
using Npgsql;

namespace Advertified.App.Services;

public sealed class AdminIndustryStrategyProfileService : IAdminIndustryStrategyProfileService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly NpgsqlDataSource _dataSource;

    public AdminIndustryStrategyProfileService(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task CreateAsync(CreateAdminIndustryStrategyProfileRequest request, CancellationToken cancellationToken)
    {
        var normalized = Normalize(request);

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        var masterIndustry = await connection.QueryFirstOrDefaultAsync<MasterIndustryLookupRow>(
            new CommandDefinition(
                @"select id as Id, label as Label
                  from master_industries
                  where lower(code) = lower(@IndustryCode);",
                new { normalized.IndustryCode },
                cancellationToken: cancellationToken));

        if (masterIndustry is null)
        {
            throw new InvalidOperationException($"Master industry '{normalized.IndustryCode}' was not found.");
        }

        var existing = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                @"select count(1)
                  from master_industry_strategy_profiles profile
                  join master_industries industry on industry.id = profile.master_industry_id
                  where lower(industry.code) = lower(@IndustryCode);",
                new { normalized.IndustryCode },
                cancellationToken: cancellationToken));

        if (existing > 0)
        {
            throw new InvalidOperationException($"An industry strategy profile for '{normalized.IndustryCode}' already exists.");
        }

        await connection.ExecuteAsync(
            new CommandDefinition(
                @"insert into master_industry_strategy_profiles (
                      master_industry_id,
                      primary_persona,
                      buying_journey,
                      trust_sensitivity,
                      default_language_biases_json,
                      default_objective,
                      funnel_shape,
                      primary_kpis_json,
                      sales_cycle,
                      preferred_channels_json,
                      base_budget_split_json,
                      geography_bias,
                      preferred_tone,
                      messaging_angle,
                      recommended_cta,
                      proof_points_json,
                      guardrails_json,
                      restricted_claim_types_json,
                      research_summary,
                      research_sources_json
                  )
                  values (
                      @MasterIndustryId,
                      @PrimaryPersona,
                      @BuyingJourney,
                      @TrustSensitivity,
                      cast(@DefaultLanguageBiasesJson as jsonb),
                      @DefaultObjective,
                      @FunnelShape,
                      cast(@PrimaryKpisJson as jsonb),
                      @SalesCycle,
                      cast(@PreferredChannelsJson as jsonb),
                      cast(@BaseBudgetSplitJson as jsonb),
                      @GeographyBias,
                      @PreferredTone,
                      @MessagingAngle,
                      @RecommendedCta,
                      cast(@ProofPointsJson as jsonb),
                      cast(@GuardrailsJson as jsonb),
                      cast(@RestrictedClaimTypesJson as jsonb),
                      @ResearchSummary,
                      cast(@ResearchSourcesJson as jsonb)
                  );",
                BuildParameters(masterIndustry.Id, normalized),
                cancellationToken: cancellationToken));
    }

    public async Task UpdateAsync(string industryCode, UpdateAdminIndustryStrategyProfileRequest request, CancellationToken cancellationToken)
    {
        var routeCode = NormalizeIndustryCode(industryCode);
        var normalized = Normalize(request);
        if (!string.Equals(routeCode, normalized.IndustryCode, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Route industry code must match request industry code.");
        }

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        var updated = await connection.ExecuteAsync(
            new CommandDefinition(
                @"update master_industry_strategy_profiles profile
                  set
                      primary_persona = @PrimaryPersona,
                      buying_journey = @BuyingJourney,
                      trust_sensitivity = @TrustSensitivity,
                      default_language_biases_json = cast(@DefaultLanguageBiasesJson as jsonb),
                      default_objective = @DefaultObjective,
                      funnel_shape = @FunnelShape,
                      primary_kpis_json = cast(@PrimaryKpisJson as jsonb),
                      sales_cycle = @SalesCycle,
                      preferred_channels_json = cast(@PreferredChannelsJson as jsonb),
                      base_budget_split_json = cast(@BaseBudgetSplitJson as jsonb),
                      geography_bias = @GeographyBias,
                      preferred_tone = @PreferredTone,
                      messaging_angle = @MessagingAngle,
                      recommended_cta = @RecommendedCta,
                      proof_points_json = cast(@ProofPointsJson as jsonb),
                      guardrails_json = cast(@GuardrailsJson as jsonb),
                      restricted_claim_types_json = cast(@RestrictedClaimTypesJson as jsonb),
                      research_summary = @ResearchSummary,
                      research_sources_json = cast(@ResearchSourcesJson as jsonb),
                      updated_at = now()
                  from master_industries industry
                  where industry.id = profile.master_industry_id
                    and lower(industry.code) = lower(@IndustryCode);",
                BuildParameters(null, normalized),
                cancellationToken: cancellationToken));

        if (updated == 0)
        {
            throw new InvalidOperationException($"Industry strategy profile '{normalized.IndustryCode}' was not found.");
        }
    }

    public async Task DeleteAsync(string industryCode, CancellationToken cancellationToken)
    {
        var normalizedCode = NormalizeIndustryCode(industryCode);
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        var deleted = await connection.ExecuteAsync(
            new CommandDefinition(
                @"delete from master_industry_strategy_profiles profile
                  using master_industries industry
                  where industry.id = profile.master_industry_id
                    and lower(industry.code) = lower(@IndustryCode);",
                new { IndustryCode = normalizedCode },
                cancellationToken: cancellationToken));

        if (deleted == 0)
        {
            throw new InvalidOperationException($"Industry strategy profile '{normalizedCode}' was not found.");
        }
    }

    private static DynamicParameters BuildParameters(Guid? masterIndustryId, NormalizedRequest normalized)
    {
        var parameters = new DynamicParameters();
        if (masterIndustryId.HasValue)
        {
            parameters.Add("MasterIndustryId", masterIndustryId.Value);
        }

        parameters.Add("IndustryCode", normalized.IndustryCode);
        parameters.Add("PrimaryPersona", normalized.PrimaryPersona);
        parameters.Add("BuyingJourney", normalized.BuyingJourney);
        parameters.Add("TrustSensitivity", normalized.TrustSensitivity);
        parameters.Add("DefaultLanguageBiasesJson", Serialize(normalized.DefaultLanguageBiases));
        parameters.Add("DefaultObjective", normalized.DefaultObjective);
        parameters.Add("FunnelShape", normalized.FunnelShape);
        parameters.Add("PrimaryKpisJson", Serialize(normalized.PrimaryKpis));
        parameters.Add("SalesCycle", normalized.SalesCycle);
        parameters.Add("PreferredChannelsJson", Serialize(normalized.PreferredChannels));
        parameters.Add("BaseBudgetSplitJson", Serialize(normalized.BaseBudgetSplit));
        parameters.Add("GeographyBias", normalized.GeographyBias);
        parameters.Add("PreferredTone", normalized.PreferredTone);
        parameters.Add("MessagingAngle", normalized.MessagingAngle);
        parameters.Add("RecommendedCta", normalized.RecommendedCta);
        parameters.Add("ProofPointsJson", Serialize(normalized.ProofPoints));
        parameters.Add("GuardrailsJson", Serialize(normalized.Guardrails));
        parameters.Add("RestrictedClaimTypesJson", Serialize(normalized.RestrictedClaimTypes));
        parameters.Add("ResearchSummary", normalized.ResearchSummary);
        parameters.Add("ResearchSourcesJson", Serialize(normalized.ResearchSources));
        return parameters;
    }

    private static NormalizedRequest Normalize(CreateAdminIndustryStrategyProfileRequest request)
    {
        var industryCode = NormalizeIndustryCode(request.IndustryCode);
        if (string.IsNullOrWhiteSpace(industryCode))
        {
            throw new InvalidOperationException("Industry code is required.");
        }

        return new NormalizedRequest(
            industryCode,
            request.IndustryLabel?.Trim() ?? string.Empty,
            request.PrimaryPersona?.Trim() ?? string.Empty,
            request.BuyingJourney?.Trim() ?? string.Empty,
            request.TrustSensitivity?.Trim() ?? string.Empty,
            NormalizeValues(request.DefaultLanguageBiases),
            request.DefaultObjective?.Trim() ?? string.Empty,
            request.FunnelShape?.Trim() ?? string.Empty,
            NormalizeValues(request.PrimaryKpis),
            request.SalesCycle?.Trim() ?? string.Empty,
            NormalizeValues(request.PreferredChannels),
            NormalizeBudgetSplit(request.BaseBudgetSplit),
            request.GeographyBias?.Trim() ?? string.Empty,
            request.PreferredTone?.Trim() ?? string.Empty,
            request.MessagingAngle?.Trim() ?? string.Empty,
            request.RecommendedCta?.Trim() ?? string.Empty,
            NormalizeValues(request.ProofPoints),
            NormalizeValues(request.Guardrails),
            NormalizeValues(request.RestrictedClaimTypes),
            request.ResearchSummary?.Trim() ?? string.Empty,
            NormalizeValues(request.ResearchSources));
    }

    private static string NormalizeIndustryCode(string? value)
    {
        return (value ?? string.Empty).Trim();
    }

    private static IReadOnlyList<string> NormalizeValues(IReadOnlyList<string>? values)
    {
        return (values ?? Array.Empty<string>())
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyDictionary<string, int> NormalizeBudgetSplit(IReadOnlyDictionary<string, int>? values)
    {
        return new Dictionary<string, int>(
            (values ?? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase))
                .Where(static entry => !string.IsNullOrWhiteSpace(entry.Key))
                .ToDictionary(entry => entry.Key.Trim(), entry => Math.Max(0, entry.Value), StringComparer.OrdinalIgnoreCase),
            StringComparer.OrdinalIgnoreCase);
    }

    private static string Serialize<T>(T value)
    {
        return JsonSerializer.Serialize(value, SerializerOptions);
    }

    private sealed record NormalizedRequest(
        string IndustryCode,
        string IndustryLabel,
        string PrimaryPersona,
        string BuyingJourney,
        string TrustSensitivity,
        IReadOnlyList<string> DefaultLanguageBiases,
        string DefaultObjective,
        string FunnelShape,
        IReadOnlyList<string> PrimaryKpis,
        string SalesCycle,
        IReadOnlyList<string> PreferredChannels,
        IReadOnlyDictionary<string, int> BaseBudgetSplit,
        string GeographyBias,
        string PreferredTone,
        string MessagingAngle,
        string RecommendedCta,
        IReadOnlyList<string> ProofPoints,
        IReadOnlyList<string> Guardrails,
        IReadOnlyList<string> RestrictedClaimTypes,
        string ResearchSummary,
        IReadOnlyList<string> ResearchSources);

    private sealed class MasterIndustryLookupRow
    {
        public Guid Id { get; init; }
        public string Label { get; init; } = string.Empty;
    }
}
