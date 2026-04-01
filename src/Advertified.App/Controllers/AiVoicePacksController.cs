using System.Text.Json;
using Advertified.App.AIPlatform.Api;
using Advertified.App.AIPlatform.Application;
using Advertified.App.Data;
using Advertified.App.Data.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Advertified.App.Controllers;

[ApiController]
[Route("api/v2/ai-platform/voice-packs")]
public sealed class AiVoicePacksController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IVoicePackPolicyService _voicePackPolicyService;

    public AiVoicePacksController(AppDbContext db, IVoicePackPolicyService voicePackPolicyService)
    {
        _db = db;
        _voicePackPolicyService = voicePackPolicyService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<VoicePackResponse>>> Get(
        [FromQuery] string? provider,
        [FromQuery] Guid? campaignId,
        [FromQuery] decimal? packageBudget,
        [FromQuery] string? campaignTier,
        CancellationToken cancellationToken)
    {
        var normalizedProvider = string.IsNullOrWhiteSpace(provider) ? "ElevenLabs" : provider.Trim();
        Guid? clientUserId = null;
        var effectiveBudget = packageBudget;
        if (campaignId.HasValue)
        {
            var campaign = await _db.Campaigns
                .AsNoTracking()
                .Where(item => item.Id == campaignId.Value)
                .Select(item => new
                {
                    item.UserId,
                    SelectedBudget = item.PackageOrder.SelectedBudget ?? item.PackageOrder.Amount
                })
                .FirstOrDefaultAsync(cancellationToken);
            if (campaign is not null)
            {
                clientUserId = campaign.UserId;
                effectiveBudget ??= campaign.SelectedBudget;
            }
        }

        var allowedTierRank = ResolveAllowedTierRank(effectiveBudget, campaignTier);
        var rows = await _db.AiVoicePacks
            .AsNoTracking()
            .Where(item => item.Provider == normalizedProvider
                           && item.IsActive
                           && (!item.IsClientSpecific || (clientUserId.HasValue && item.ClientUserId == clientUserId.Value)))
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.Name)
            .ToArrayAsync(cancellationToken);

        var filtered = rows
            .Where(item => ResolveTierRank(item.PricingTier) <= allowedTierRank)
            .Select(Map)
            .ToArray();

        return Ok(filtered);
    }

    [HttpGet("recommendation")]
    public async Task<ActionResult<VoicePackRecommendationResponse>> Recommend(
        [FromQuery] Guid campaignId,
        [FromQuery] string? provider,
        [FromQuery] string? audience,
        [FromQuery] string? objective,
        [FromQuery] decimal? packageBudget,
        [FromQuery] string? campaignTier,
        CancellationToken cancellationToken)
    {
        var normalizedProvider = string.IsNullOrWhiteSpace(provider) ? "ElevenLabs" : provider.Trim();
        var recommendation = await _voicePackPolicyService.RecommendAsync(
            campaignId,
            normalizedProvider,
            audience,
            objective,
            packageBudget,
            campaignTier,
            cancellationToken);

        if (recommendation is null)
        {
            return NotFound();
        }

        return Ok(new VoicePackRecommendationResponse
        {
            VoicePackId = recommendation.VoicePackId,
            Reason = recommendation.Reason,
            MatchScore = recommendation.MatchScore
        });
    }

    private static VoicePackResponse Map(AiVoicePack row)
    {
        return new VoicePackResponse
        {
            Id = row.Id,
            Provider = row.Provider,
            Name = row.Name,
            Accent = row.Accent,
            Language = row.Language,
            Tone = row.Tone,
            Persona = row.Persona,
            UseCases = DeserializeList(row.UseCasesJson),
            SampleAudioUrl = row.SampleAudioUrl,
            PromptTemplate = row.PromptTemplate,
            PricingTier = row.PricingTier,
            IsClientSpecific = row.IsClientSpecific,
            IsClonedVoice = row.IsClonedVoice,
            AudienceTags = DeserializeList(row.AudienceTagsJson),
            ObjectiveTags = DeserializeList(row.ObjectiveTagsJson),
            SortOrder = row.SortOrder
        };
    }

    private static int ResolveTierRank(string tier)
    {
        return tier.Trim().ToLowerInvariant() switch
        {
            "exclusive" => 3,
            "premium" => 2,
            _ => 1
        };
    }

    private static int ResolveAllowedTierRank(decimal? packageBudget, string? campaignTier)
    {
        var tierRank = campaignTier?.Trim().ToLowerInvariant() switch
        {
            "exclusive" => 3,
            "premium" => 2,
            "standard" => 1,
            _ => 0
        };

        if (!packageBudget.HasValue)
        {
            return Math.Max(1, tierRank);
        }

        var budgetRank = packageBudget.Value switch
        {
            < 50000m => 1,
            < 150000m => 2,
            _ => 3
        };

        return Math.Max(tierRank, budgetRank);
    }

    private static string[] DeserializeList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Array.Empty<string>();
        }

        try
        {
            return JsonSerializer.Deserialize<string[]>(json) ?? Array.Empty<string>();
        }
        catch (JsonException)
        {
            return Array.Empty<string>();
        }
    }
}
