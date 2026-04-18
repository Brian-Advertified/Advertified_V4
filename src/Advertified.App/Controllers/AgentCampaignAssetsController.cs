using Advertified.App.Contracts.Agent;
using Advertified.App.Contracts.Campaigns;
using Advertified.App.Data;
using Advertified.App.Data.Entities;
using Advertified.App.Data.Enums;
using Advertified.App.Services.Abstractions;
using Advertified.App.Support;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Advertified.App.Controllers;

/// <summary>
/// Handles campaign asset uploads and management for agent workflows.
/// Assets are media files (images, videos, documents) attached to campaigns.
/// </summary>
[ApiController]
[Route("agent/campaigns")]
[Authorize(Roles = "Agent,Admin")]
public sealed class AgentCampaignAssetsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IAgentCampaignOwnershipService _ownershipService;
    private readonly IPublicAssetStorage _assetStorage;
    private readonly IChangeAuditService _changeAuditService;
    private readonly ILogger<AgentCampaignAssetsController> _logger;

    public AgentCampaignAssetsController(
        AppDbContext db,
        ICurrentUserAccessor currentUserAccessor,
        IAgentCampaignOwnershipService ownershipService,
        IPublicAssetStorage assetStorage,
        IChangeAuditService changeAuditService,
        ILogger<AgentCampaignAssetsController> logger)
    {
        _db = db;
        _currentUserAccessor = currentUserAccessor;
        _ownershipService = ownershipService;
        _assetStorage = assetStorage;
        _changeAuditService = changeAuditService;
        _logger = logger;
    }

    [HttpPost("{id:guid}/assets")]
    [RequestSizeLimit(25_000_000)]
    public async Task<ActionResult<CampaignAssetResponse>> UploadAsset(
        Guid id,
        [FromForm] IFormFile file,
        [FromForm] string? assetType,
        CancellationToken cancellationToken)
    {
        var currentUser = await GetCurrentOperationsUserAsync(cancellationToken);
        var campaign = await _ownershipService.GetOwnedCampaignAsync(
            id,
            currentUser,
            query => query,
            cancellationToken);

        if (file.Length <= 0)
        {
            return BadRequest(new { message = "Select a file to upload." });
        }

        byte[] bytes;
        await using (var stream = file.OpenReadStream())
        await using (var memory = new MemoryStream())
        {
            await stream.CopyToAsync(memory, cancellationToken);
            bytes = memory.ToArray();
        }

        var safeFileName = Path.GetFileName(file.FileName);
        var normalizedAssetType = string.IsNullOrWhiteSpace(assetType) ? "operations_asset" : assetType.Trim().ToLowerInvariant();
        var objectKey = $"campaigns/{campaign.Id:D}/assets/{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}-{safeFileName}";
        var savedKey = await _assetStorage.SaveAsync(objectKey, bytes, file.ContentType ?? "application/octet-stream", cancellationToken);

        var asset = new CampaignAsset
        {
            Id = Guid.NewGuid(),
            CampaignId = campaign.Id,
            UploadedByUserId = currentUser.Id,
            AssetType = normalizedAssetType,
            DisplayName = safeFileName,
            StorageObjectKey = savedKey,
            PublicUrl = _assetStorage.GetPublicUrl(savedKey),
            ContentType = file.ContentType,
            SizeBytes = file.Length,
            CreatedAt = DateTime.UtcNow
        };

        _db.CampaignAssets.Add(asset);
        campaign.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        await WriteChangeAuditAsync(
            "upload_campaign_asset",
            "campaign_asset",
            asset.Id.ToString(),
            ResolveCampaignLabel(campaign),
            $"Uploaded {asset.DisplayName} for {ResolveCampaignLabel(campaign)}.",
            new { CampaignId = campaign.Id, AssetType = asset.AssetType, asset.DisplayName, asset.SizeBytes },
            cancellationToken);

        return Ok(new CampaignAssetResponse
        {
            Id = asset.Id,
            AssetType = asset.AssetType,
            DisplayName = asset.DisplayName,
            PublicUrl = asset.PublicUrl ?? $"/campaign-assets/{asset.Id}",
            ContentType = asset.ContentType,
            SizeBytes = asset.SizeBytes,
            CreatedAt = new DateTimeOffset(asset.CreatedAt, TimeSpan.Zero)
        });
    }

    private async Task WriteChangeAuditAsync(
        string action,
        string entityType,
        string entityId,
        string? entityLabel,
        string summary,
        object? metadata,
        CancellationToken cancellationToken)
    {
        var currentUserId = await _currentUserAccessor.GetCurrentUserIdAsync(cancellationToken);
        await _changeAuditService.WriteAsync(currentUserId, "agent", action, entityType, entityId, entityLabel, summary, metadata, cancellationToken);
    }

    private async Task<UserAccount> GetCurrentOperationsUserAsync(CancellationToken cancellationToken)
    {
        var currentUserId = await _currentUserAccessor.GetCurrentUserIdAsync(cancellationToken);
        var currentUser = await _db.UserAccounts.FirstOrDefaultAsync(x => x.Id == currentUserId, cancellationToken);
        if (currentUser is null)
        {
            throw new InvalidOperationException("Authenticated user account could not be found.");
        }

        if (currentUser.Role is not UserRole.Agent and not UserRole.Admin and not UserRole.CreativeDirector)
        {
            throw new ForbiddenException("Agent, creative director, or admin access is required.");
        }

        return currentUser;
    }

    private static string ResolveCampaignLabel(Campaign campaign)
    {
        return string.IsNullOrWhiteSpace(campaign.CampaignName)
            ? $"{campaign.PackageBand?.Name ?? "Campaign"} campaign"
            : campaign.CampaignName.Trim();
    }
}
