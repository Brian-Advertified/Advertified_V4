using Advertified.App.AIPlatform.Api;
using Advertified.App.AIPlatform.Application;
using Advertified.App.AIPlatform.Domain;
using Advertified.App.Data;
using Advertified.App.Data.Entities;
using Advertified.App.Data.Enums;
using Advertified.App.Services.Abstractions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Advertified.App.Controllers;

[ApiController]
[Route("api/v2/ai-platform/ad-ops")]
public sealed class AiAdOperationsController : ControllerBase
{
    private readonly IAdVariantService _adVariantService;
    private readonly AppDbContext _db;
    private readonly ICurrentUserAccessor _currentUserAccessor;

    public AiAdOperationsController(
        IAdVariantService adVariantService,
        AppDbContext db,
        ICurrentUserAccessor currentUserAccessor)
    {
        _adVariantService = adVariantService;
        _db = db;
        _currentUserAccessor = currentUserAccessor;
    }

    [HttpPost("variants")]
    public async Task<ActionResult<AdVariantResponse>> CreateVariant(
        [FromBody] CreateAdVariantRequest request,
        CancellationToken cancellationToken)
    {
        var currentUser = await GetCurrentUserAsync(cancellationToken);
        if (currentUser is null)
        {
            return Unauthorized();
        }

        if (request.CampaignId == Guid.Empty)
        {
            throw new InvalidOperationException("campaignId is required.");
        }

        var accessResult = await EnsureCampaignAccessAsync(currentUser, request.CampaignId, allowClientPublishActions: false, cancellationToken: cancellationToken);
        if (accessResult is not null)
        {
            return accessResult;
        }

        var created = await _adVariantService.CreateVariantAsync(
            new CreateAdVariantCommand(
                request.CampaignId,
                request.CampaignCreativeId,
                request.Platform,
                request.Channel,
                request.Language,
                request.TemplateId,
                request.VoicePackId,
                request.VoicePackName,
                request.Script,
                request.AudioAssetUrl),
            cancellationToken);

        return Ok(MapVariant(created));
    }

    [HttpGet("campaigns/{campaignId:guid}/variants")]
    public async Task<ActionResult<IReadOnlyList<AdVariantResponse>>> GetCampaignVariants(
        Guid campaignId,
        CancellationToken cancellationToken)
    {
        var currentUser = await GetCurrentUserAsync(cancellationToken);
        if (currentUser is null)
        {
            return Unauthorized();
        }

        var accessResult = await EnsureCampaignAccessAsync(currentUser, campaignId, allowClientPublishActions: false, cancellationToken: cancellationToken);
        if (accessResult is not null)
        {
            return accessResult;
        }

        var rows = await _adVariantService.GetCampaignVariantsAsync(campaignId, cancellationToken);
        return Ok(rows.Select(MapVariant).ToArray());
    }

    [HttpPost("variants/{variantId:guid}/publish")]
    public async Task<ActionResult<PublishAdVariantResponse>> PublishVariant(
        Guid variantId,
        CancellationToken cancellationToken)
    {
        var currentUser = await GetCurrentUserAsync(cancellationToken);
        if (currentUser is null)
        {
            return Unauthorized();
        }

        var (campaignId, variantResolveResult) = await ResolveCampaignIdForVariantAsync(variantId, cancellationToken);
        if (variantResolveResult is not null)
        {
            return variantResolveResult;
        }

        var accessResult = await EnsureCampaignAccessAsync(currentUser, campaignId!.Value, allowClientPublishActions: true, cancellationToken: cancellationToken);
        if (accessResult is not null)
        {
            return accessResult;
        }

        var result = await _adVariantService.PublishVariantAsync(variantId, cancellationToken);
        return Ok(new PublishAdVariantResponse
        {
            VariantId = result.VariantId,
            CampaignId = result.CampaignId,
            Platform = result.Platform,
            PlatformAdId = result.PlatformAdId,
            Status = result.Status,
            PublishedAt = result.PublishedAt
        });
    }

    [HttpPost("variants/{variantId:guid}/conversions")]
    public async Task<IActionResult> TrackConversion(
        Guid variantId,
        [FromBody] TrackConversionRequest request,
        CancellationToken cancellationToken)
    {
        var currentUser = await GetCurrentUserAsync(cancellationToken);
        if (currentUser is null)
        {
            return Unauthorized();
        }

        var (campaignId, variantResolveResult) = await ResolveCampaignIdForVariantAsync(variantId, cancellationToken);
        if (variantResolveResult is not null)
        {
            return variantResolveResult;
        }

        var accessResult = await EnsureCampaignAccessAsync(currentUser, campaignId!.Value, allowClientPublishActions: false, cancellationToken: cancellationToken);
        if (accessResult is not null)
        {
            return accessResult;
        }

        var count = request.Conversions <= 0 ? 1 : request.Conversions;
        await _adVariantService.RecordConversionAsync(variantId, count, cancellationToken);
        return Accepted(new { VariantId = variantId, Conversions = count });
    }

    [HttpGet("campaigns/{campaignId:guid}/metrics/summary")]
    public async Task<ActionResult<CampaignAdMetricsSummaryResponse>> GetCampaignMetricsSummary(
        Guid campaignId,
        CancellationToken cancellationToken)
    {
        var currentUser = await GetCurrentUserAsync(cancellationToken);
        if (currentUser is null)
        {
            return Unauthorized();
        }

        var accessResult = await EnsureCampaignAccessAsync(currentUser, campaignId, allowClientPublishActions: false, cancellationToken: cancellationToken);
        if (accessResult is not null)
        {
            return accessResult;
        }

        var summary = await _adVariantService.GetCampaignMetricsSummaryAsync(campaignId, cancellationToken);
        return Ok(MapSummary(summary));
    }

    [HttpPost("campaigns/{campaignId:guid}/sync-metrics")]
    public async Task<ActionResult<SyncCampaignMetricsResponse>> SyncCampaignMetrics(
        Guid campaignId,
        CancellationToken cancellationToken)
    {
        var currentUser = await GetCurrentUserAsync(cancellationToken);
        if (currentUser is null)
        {
            return Unauthorized();
        }

        var accessResult = await EnsureCampaignAccessAsync(currentUser, campaignId, allowClientPublishActions: true, cancellationToken: cancellationToken);
        if (accessResult is not null)
        {
            return accessResult;
        }

        var result = await _adVariantService.SyncCampaignMetricsAsync(campaignId, cancellationToken);
        return Ok(new SyncCampaignMetricsResponse
        {
            CampaignId = result.CampaignId,
            SyncedVariantCount = result.SyncedVariantCount,
            Summary = MapSummary(result.Summary)
        });
    }

    [HttpPost("campaigns/{campaignId:guid}/optimize")]
    public async Task<ActionResult<OptimizeCampaignResponse>> OptimizeCampaign(
        Guid campaignId,
        CancellationToken cancellationToken)
    {
        var currentUser = await GetCurrentUserAsync(cancellationToken);
        if (currentUser is null)
        {
            return Unauthorized();
        }

        var accessResult = await EnsureCampaignAccessAsync(currentUser, campaignId, allowClientPublishActions: true, cancellationToken: cancellationToken);
        if (accessResult is not null)
        {
            return accessResult;
        }

        var result = await _adVariantService.OptimizeCampaignAsync(campaignId, cancellationToken);
        return Ok(new OptimizeCampaignResponse
        {
            CampaignId = result.CampaignId,
            PromotedVariantId = result.PromotedVariantId,
            Message = result.Message,
            OptimizedAt = result.OptimizedAt
        });
    }

    private static AdVariantResponse MapVariant(AdVariantSummary item)
    {
        return new AdVariantResponse
        {
            Id = item.Id,
            CampaignId = item.CampaignId,
            CampaignCreativeId = item.CampaignCreativeId,
            Platform = item.Platform,
            Channel = item.Channel,
            Language = item.Language,
            TemplateId = item.TemplateId,
            VoicePackId = item.VoicePackId,
            VoicePackName = item.VoicePackName,
            Script = item.Script,
            AudioAssetUrl = item.AudioAssetUrl,
            PlatformAdId = item.PlatformAdId,
            Status = item.Status,
            CreatedAt = item.CreatedAt,
            UpdatedAt = item.UpdatedAt,
            PublishedAt = item.PublishedAt
        };
    }

    private static CampaignAdMetricsSummaryResponse MapSummary(CampaignAdMetricsSummary summary)
    {
        return new CampaignAdMetricsSummaryResponse
        {
            CampaignId = summary.CampaignId,
            VariantCount = summary.VariantCount,
            PublishedVariantCount = summary.PublishedVariantCount,
            Impressions = summary.Impressions,
            Clicks = summary.Clicks,
            Conversions = summary.Conversions,
            CostZar = summary.CostZar,
            Ctr = summary.Ctr,
            ConversionRate = summary.ConversionRate,
            TopVariantId = summary.TopVariantId,
            TopVariantConversionRate = summary.TopVariantConversionRate,
            LastRecordedAt = summary.LastRecordedAt
        };
    }

    private async Task<UserAccount?> GetCurrentUserAsync(CancellationToken cancellationToken)
    {
        Guid currentUserId;
        try
        {
            currentUserId = await _currentUserAccessor.GetCurrentUserIdAsync(cancellationToken);
        }
        catch (InvalidOperationException)
        {
            return null;
        }

        return await _db.UserAccounts
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == currentUserId, cancellationToken);
    }

    private async Task<(Guid? CampaignId, ActionResult? ErrorResult)> ResolveCampaignIdForVariantAsync(Guid variantId, CancellationToken cancellationToken)
    {
        var campaignId = await _db.AiAdVariants
            .AsNoTracking()
            .Where(item => item.Id == variantId)
            .Select(item => (Guid?)item.CampaignId)
            .FirstOrDefaultAsync(cancellationToken);

        return campaignId.HasValue
            ? (campaignId.Value, null)
            : (null, NotFound(new { message = "Ad variant not found." }));
    }

    private async Task<ActionResult?> EnsureCampaignAccessAsync(
        UserAccount user,
        Guid campaignId,
        bool allowClientPublishActions,
        CancellationToken cancellationToken)
    {
        var campaign = await _db.Campaigns
            .AsNoTracking()
            .Select(item => new { item.Id, item.UserId, item.AssignedAgentUserId })
            .FirstOrDefaultAsync(item => item.Id == campaignId, cancellationToken);
        if (campaign is null)
        {
            return NotFound(new { message = "Campaign not found." });
        }

        if (user.Role == UserRole.Admin || user.Role == UserRole.CreativeDirector)
        {
            return null;
        }

        if (user.Role == UserRole.Agent)
        {
            // Agents only manage their assigned campaigns.
            if (campaign.AssignedAgentUserId == user.Id)
            {
                return null;
            }

            return StatusCode(StatusCodes.Status403Forbidden);
        }

        if (user.Role == UserRole.Client)
        {
            if (allowClientPublishActions)
            {
                return StatusCode(StatusCodes.Status403Forbidden);
            }

            if (campaign.UserId == user.Id)
            {
                return null;
            }

            return StatusCode(StatusCodes.Status403Forbidden);
        }

        return StatusCode(StatusCodes.Status403Forbidden);
    }
}
