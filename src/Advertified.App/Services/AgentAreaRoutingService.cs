using System.Data;
using System.Globalization;
using System.Text.Json;
using Advertified.App.Configuration;
using Advertified.App.Data;
using Advertified.App.Data.Entities;
using Advertified.App.Services.Abstractions;
using Advertified.App.Support;
using Dapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Advertified.App.Services;

public sealed class AgentAreaRoutingService : IAgentAreaRoutingService
{
    private readonly AppDbContext _db;
    private readonly ITemplatedEmailService _emailService;
    private readonly IChangeAuditService _changeAuditService;
    private readonly FrontendOptions _frontendOptions;
    private readonly ILogger<AgentAreaRoutingService> _logger;

    public AgentAreaRoutingService(
        AppDbContext db,
        ITemplatedEmailService emailService,
        IChangeAuditService changeAuditService,
        IOptions<FrontendOptions> frontendOptions,
        ILogger<AgentAreaRoutingService> logger)
    {
        _db = db;
        _emailService = emailService;
        _changeAuditService = changeAuditService;
        _frontendOptions = frontendOptions.Value;
        _logger = logger;
    }

    public async Task TryAssignCampaignAsync(Guid campaignId, string trigger, CancellationToken cancellationToken)
    {
        var campaign = await _db.Campaigns
            .Include(x => x.User!)
                .ThenInclude(x => x.BusinessProfile)
            .Include(x => x.ProspectLead)
            .Include(x => x.PackageBand)
            .Include(x => x.PackageOrder)
            .Include(x => x.CampaignBrief)
            .FirstOrDefaultAsync(x => x.Id == campaignId, cancellationToken);

        if (campaign is null || campaign.AssignedAgentUserId.HasValue)
        {
            return;
        }

        var territoryRows = await LoadTerritoryRowsAsync(cancellationToken);
        if (territoryRows.Count == 0)
        {
            return;
        }

        var resolution = ResolveBestMatch(campaign, territoryRows);
        if (resolution is null)
        {
            return;
        }

        var agent = await _db.UserAccounts
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == resolution.AgentUserId, cancellationToken);

        if (agent is null)
        {
            return;
        }

        campaign.AssignedAgentUserId = resolution.AgentUserId;
        campaign.AssignedAt = DateTime.UtcNow;
        campaign.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        await _changeAuditService.WriteAsync(
            null,
            "system",
            "auto_assign_agent_by_area",
            "campaign",
            campaign.Id.ToString(),
            ResolveCampaignName(campaign),
            $"Automatically assigned {ResolveCampaignName(campaign)} to {agent.FullName} for area {resolution.AreaLabel}.",
            new
            {
                CampaignId = campaign.Id,
                AgentUserId = agent.Id,
                AgentName = agent.FullName,
                resolution.AreaCode,
                resolution.AreaLabel,
                trigger
            },
            cancellationToken);

        await SendAgentAssignmentEmailAsync(campaign, agent, resolution, cancellationToken);
    }

    private async Task<IReadOnlyList<TerritoryRow>> LoadTerritoryRowsAsync(CancellationToken cancellationToken)
    {
        const string sql = @"
            select
                aaa.agent_user_id as AgentUserId,
                aaa.area_code as AreaCode,
                pap.display_name as AreaLabel,
                coalesce(pap.fallback_locations_json, '[]'::jsonb)::text as FallbackLocationsJson,
                rcm.province as Province,
                rcm.city as City
            from agent_area_assignments aaa
            join package_area_profiles pap on pap.cluster_code = aaa.area_code
            left join region_clusters rc on rc.code = pap.cluster_code
            left join region_cluster_mappings rcm on rcm.cluster_id = rc.id
            where pap.is_active = true;";

        var connection = _db.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            var rows = await connection.QueryAsync<TerritoryRow>(new CommandDefinition(sql, cancellationToken: cancellationToken));
            return rows.ToArray();
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static TerritoryResolution? ResolveBestMatch(Campaign campaign, IReadOnlyList<TerritoryRow> rows)
    {
        var grouped = rows
            .GroupBy(x => new { x.AgentUserId, x.AreaCode, x.AreaLabel, x.FallbackLocationsJson })
            .Select(group => new TerritoryCandidate
            {
                AgentUserId = group.Key.AgentUserId,
                AreaCode = group.Key.AreaCode,
                AreaLabel = group.Key.AreaLabel,
                Tokens = BuildCandidateTokens(group.Key.AreaCode, group.Key.AreaLabel, group.Key.FallbackLocationsJson, group.Select(x => x.Province), group.Select(x => x.City))
            })
            .ToArray();

        var searchTerms = BuildSearchTerms(campaign);
        TerritoryResolution? best = null;

        foreach (var candidate in grouped)
        {
            var score = ScoreMatch(searchTerms, candidate.Tokens);
            if (score <= 0)
            {
                continue;
            }

            if (best is null || score > best.Score)
            {
                best = new TerritoryResolution
                {
                    AgentUserId = candidate.AgentUserId,
                    AreaCode = candidate.AreaCode,
                    AreaLabel = candidate.AreaLabel,
                    Score = score
                };
            }
        }

        return best;
    }

    private static HashSet<string> BuildSearchTerms(Campaign campaign)
    {
        var terms = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AddTerms(terms, DeserializeList(campaign.CampaignBrief?.AreasJson));
        AddTerms(terms, DeserializeList(campaign.CampaignBrief?.MustHaveAreasJson));
        AddTerms(terms, DeserializeList(campaign.CampaignBrief?.CitiesJson));
        AddTerms(terms, DeserializeList(campaign.CampaignBrief?.ProvincesJson));

        if (campaign.User?.BusinessProfile is not null)
        {
            AddTerm(terms, campaign.User.BusinessProfile.City);
            AddTerm(terms, campaign.User.BusinessProfile.Province);
        }

        return terms;
    }

    private static HashSet<string> BuildCandidateTokens(string areaCode, string areaLabel, string? fallbackLocationsJson, IEnumerable<string?> provinces, IEnumerable<string?> cities)
    {
        var tokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AddTerm(tokens, areaCode);
        AddTerm(tokens, areaLabel);
        AddTerms(tokens, DeserializeList(fallbackLocationsJson));

        foreach (var province in provinces)
        {
            AddTerm(tokens, province);
        }

        foreach (var city in cities)
        {
            AddTerm(tokens, city);
        }

        return tokens;
    }

    private static int ScoreMatch(IEnumerable<string> searchTerms, IEnumerable<string> candidateTokens)
    {
        var score = 0;
        var normalizedCandidates = candidateTokens.ToArray();

        foreach (var term in searchTerms)
        {
            foreach (var token in normalizedCandidates)
            {
                if (term.Equals(token, StringComparison.OrdinalIgnoreCase))
                {
                    score += 100;
                    continue;
                }

                if (token.Contains(term, StringComparison.OrdinalIgnoreCase) || term.Contains(token, StringComparison.OrdinalIgnoreCase))
                {
                    score += 40;
                }
            }
        }

        return score;
    }

    private async Task SendAgentAssignmentEmailAsync(Campaign campaign, UserAccount agent, TerritoryResolution resolution, CancellationToken cancellationToken)
    {
        try
        {
            await _emailService.SendAsync(
                "agent-area-campaign-assigned",
                agent.Email,
                "campaigns",
                new Dictionary<string, string?>
                {
                    ["AgentName"] = agent.FullName,
                    ["ClientName"] = campaign.ResolveClientName(),
                    ["CampaignName"] = ResolveCampaignName(campaign),
                    ["PackageName"] = campaign.PackageBand.Name,
                    ["Budget"] = FormatCurrency(campaign.PackageOrder.SelectedBudget ?? campaign.PackageOrder.Amount),
                    ["AreaName"] = resolution.AreaLabel,
                    ["AreaCode"] = resolution.AreaCode,
                    ["AgentCampaignUrl"] = BuildFrontendUrl($"/agent/campaigns/{campaign.Id}")
                },
                null,
                null,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send area assignment email for campaign {CampaignId} to agent {AgentUserId}.", campaign.Id, agent.Id);
        }
    }

    private static IReadOnlyList<string> DeserializeList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Array.Empty<string>();
        }

        return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
    }

    private static void AddTerms(HashSet<string> target, IEnumerable<string> values)
    {
        foreach (var value in values)
        {
            AddTerm(target, value);
        }
    }

    private static void AddTerm(HashSet<string> target, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        var normalized = value.Trim();
        if (normalized.Length == 0)
        {
            return;
        }

        target.Add(normalized);
    }

    private string BuildFrontendUrl(string path)
    {
        return _frontendOptions.BaseUrl.TrimEnd('/') + path;
    }

    private static string ResolveCampaignName(Campaign campaign)
    {
        return string.IsNullOrWhiteSpace(campaign.CampaignName)
            ? $"{campaign.PackageBand.Name} campaign"
            : campaign.CampaignName.Trim();
    }

    private static string FormatCurrency(decimal amount)
    {
        return $"R {amount.ToString("N2", CultureInfo.GetCultureInfo("en-ZA"))}";
    }

    private sealed class TerritoryRow
    {
        public Guid AgentUserId { get; set; }
        public string AreaCode { get; set; } = string.Empty;
        public string AreaLabel { get; set; } = string.Empty;
        public string? FallbackLocationsJson { get; set; }
        public string? Province { get; set; }
        public string? City { get; set; }
    }

    private sealed class TerritoryCandidate
    {
        public Guid AgentUserId { get; set; }
        public string AreaCode { get; set; } = string.Empty;
        public string AreaLabel { get; set; } = string.Empty;
        public HashSet<string> Tokens { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    private sealed class TerritoryResolution
    {
        public Guid AgentUserId { get; set; }
        public string AreaCode { get; set; } = string.Empty;
        public string AreaLabel { get; set; } = string.Empty;
        public int Score { get; set; }
    }
}
