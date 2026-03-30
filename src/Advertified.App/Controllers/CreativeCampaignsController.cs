using Advertified.App.Data;
using Advertified.App.Data.Entities;
using Advertified.App.Data.Enums;
using Advertified.App.Contracts.Agent;
using Advertified.App.Contracts.Campaigns;
using Advertified.App.Contracts.Creative;
using Advertified.App.Services.Abstractions;
using Advertified.App.Support;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Advertified.App.Controllers;

[ApiController]
[Route("creative/campaigns")]
public sealed class CreativeCampaignsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IChangeAuditService _changeAuditService;
    private readonly IPublicAssetStorage _assetStorage;
    private readonly ICreativeStudioIntelligenceService _creativeStudioIntelligenceService;

    public CreativeCampaignsController(
        AppDbContext db,
        ICurrentUserAccessor currentUserAccessor,
        IChangeAuditService changeAuditService,
        IPublicAssetStorage assetStorage,
        ICreativeStudioIntelligenceService creativeStudioIntelligenceService)
    {
        _db = db;
        _currentUserAccessor = currentUserAccessor;
        _changeAuditService = changeAuditService;
        _assetStorage = assetStorage;
        _creativeStudioIntelligenceService = creativeStudioIntelligenceService;
    }

    [HttpGet("inbox")]
    public async Task<ActionResult<AgentInboxResponse>> GetInbox(CancellationToken cancellationToken)
    {
        await GetCurrentCreativeUserAsync(cancellationToken);

        var campaigns = await _db.Campaigns
            .AsNoTracking()
            .Include(x => x.User)
            .Include(x => x.AssignedAgentUser)
            .Include(x => x.PackageBand)
            .Include(x => x.PackageOrder)
            .Include(x => x.CampaignBrief)
            .Include(x => x.CampaignRecommendations)
            .Where(x =>
                x.Status == CampaignStatuses.Approved ||
                x.Status == CampaignStatuses.CreativeChangesRequested ||
                x.Status == CampaignStatuses.CreativeSentToClientForApproval ||
                x.Status == CampaignStatuses.CreativeApproved)
            .OrderByDescending(x => x.UpdatedAt)
            .ThenByDescending(x => x.CreatedAt)
            .ToArrayAsync(cancellationToken);

        var items = campaigns.Select(campaign => new AgentInboxItemResponse
        {
            Id = campaign.Id,
            UserId = campaign.UserId,
            CampaignName = ResolveCampaignLabel(campaign),
            ClientName = campaign.User.FullName,
            ClientEmail = campaign.User.Email,
            PackageBandName = campaign.PackageBand.Name,
            SelectedBudget = PricingPolicy.ResolvePlanningBudget(
                campaign.PackageOrder.SelectedBudget ?? campaign.PackageOrder.Amount,
                campaign.PackageOrder.AiStudioReserveAmount),
            Status = campaign.Status,
            PlanningMode = campaign.PlanningMode,
            QueueStage = "creative_queue",
            QueueLabel = campaign.Status == CampaignStatuses.CreativeSentToClientForApproval ? "Awaiting client approval" : "Creative production",
            AssignedAgentUserId = campaign.AssignedAgentUserId,
            AssignedAgentName = campaign.AssignedAgentUser?.FullName,
            AssignedAt = campaign.AssignedAt.HasValue ? new DateTimeOffset(campaign.AssignedAt.Value, TimeSpan.Zero) : null,
            IsAssignedToCurrentUser = false,
            IsUnassigned = campaign.AssignedAgentUserId is null,
            NextAction = campaign.Status switch
            {
                CampaignStatuses.Approved => "Open the studio and start building the creative system.",
                CampaignStatuses.CreativeChangesRequested => "Revise the creative pack and prepare a fresh client handoff.",
                CampaignStatuses.CreativeSentToClientForApproval => "Monitor client sign-off and handle any final revision notes.",
                CampaignStatuses.CreativeApproved => "Creative is approved. Operations can now activate the campaign.",
                _ => "Review the creative workload."
            },
            ManualReviewRequired = false,
            IsOverBudget = false,
            IsStale = false,
            IsUrgent = campaign.Status is CampaignStatuses.Approved or CampaignStatuses.CreativeChangesRequested,
            AgeInDays = Math.Max(0, (int)Math.Floor((DateTimeOffset.UtcNow - new DateTimeOffset(campaign.UpdatedAt, TimeSpan.Zero)).TotalDays)),
            HasBrief = campaign.CampaignBrief is not null,
            HasRecommendation = campaign.CampaignRecommendations.Any(),
            CreatedAt = new DateTimeOffset(campaign.CreatedAt, TimeSpan.Zero),
            UpdatedAt = new DateTimeOffset(campaign.UpdatedAt, TimeSpan.Zero)
        }).ToArray();

        return Ok(new AgentInboxResponse
        {
            TotalCampaigns = items.Length,
            AssignedToMeCount = 0,
            UnassignedCount = items.Count(x => x.IsUnassigned),
            UrgentCount = items.Count(x => x.IsUrgent),
            ManualReviewCount = 0,
            OverBudgetCount = 0,
            StaleCount = 0,
            NewlyPaidCount = 0,
            BriefWaitingCount = 0,
            PlanningReadyCount = items.Count(x => x.Status == CampaignStatuses.Approved),
            AgentReviewCount = items.Count(x => x.Status == CampaignStatuses.CreativeChangesRequested),
            ReadyToSendCount = items.Count(x => x.Status == CampaignStatuses.CreativeSentToClientForApproval),
            WaitingOnClientCount = items.Count(x => x.Status == CampaignStatuses.CreativeSentToClientForApproval),
            CompletedCount = items.Count(x => x.Status == CampaignStatuses.CreativeApproved),
            Items = items
        });
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CampaignDetailResponse>> GetById(Guid id, CancellationToken cancellationToken)
    {
        await GetCurrentCreativeUserAsync(cancellationToken);

        var campaign = await _db.Campaigns
            .AsNoTracking()
            .AsSplitQuery()
            .Include(x => x.User)
                .ThenInclude(x => x.BusinessProfile)
            .Include(x => x.AssignedAgentUser)
            .Include(x => x.PackageBand)
            .Include(x => x.PackageOrder)
            .Include(x => x.CampaignBrief)
            .Include(x => x.CampaignCreativeSystems)
            .Include(x => x.CampaignAssets)
            .Include(x => x.CampaignSupplierBookings)
                .ThenInclude(x => x.ProofAsset)
            .Include(x => x.CampaignDeliveryReports)
                .ThenInclude(x => x.EvidenceAsset)
            .Include(x => x.CampaignPauseWindows)
            .Include(x => x.CampaignRecommendations)
                .ThenInclude(x => x.RecommendationItems)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (campaign is null)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Campaign not found.",
                Status = StatusCodes.Status404NotFound
            });
        }

        return Ok(campaign.ToDetail());
    }

    [HttpPost("{id:guid}/assets")]
    [RequestSizeLimit(25_000_000)]
    public async Task<ActionResult<CampaignAssetResponse>> UploadAsset(
        Guid id,
        [FromForm] IFormFile file,
        [FromForm] string? assetType,
        CancellationToken cancellationToken)
    {
        var currentUser = await GetCurrentCreativeUserAsync(cancellationToken);
        var campaign = await _db.Campaigns
            .Include(x => x.PackageBand)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("Campaign not found.");

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
        var normalizedAssetType = string.IsNullOrWhiteSpace(assetType) ? "creative_pack" : assetType.Trim().ToLowerInvariant();
        var objectKey = $"campaigns/{campaign.Id:D}/creative/{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}-{safeFileName}";
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
            "upload_creative_asset",
            "campaign_asset",
            asset.Id.ToString(),
            ResolveCampaignLabel(campaign),
            $"Uploaded creative asset for {ResolveCampaignLabel(campaign)}.",
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

    [HttpPost("{id:guid}/send-finished-media-to-client")]
    public async Task<IActionResult> SendFinishedMediaToClient(Guid id, CancellationToken cancellationToken)
    {
        await GetCurrentCreativeUserAsync(cancellationToken);
        var campaign = await _db.Campaigns
            .Include(x => x.PackageBand)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("Campaign not found.");

        if (!string.Equals(campaign.Status, CampaignStatuses.Approved, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(campaign.Status, CampaignStatuses.CreativeChangesRequested, StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Campaign is not ready for creative approval handoff.",
                Detail = "Finished media can only be sent to the client after the recommendation has been approved and while creative production or creative revision is active.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        campaign.Status = CampaignStatuses.CreativeSentToClientForApproval;
        campaign.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        await WriteChangeAuditAsync(
            "send_finished_media_to_client",
            "campaign",
            campaign.Id.ToString(),
            ResolveCampaignLabel(campaign),
            $"Sent finished media to client for approval for {ResolveCampaignLabel(campaign)}.",
            new
            {
                CampaignId = campaign.Id,
                CampaignStatus = campaign.Status
            },
            cancellationToken);

        return Accepted(new
        {
            CampaignId = campaign.Id,
            Status = campaign.Status,
            Message = "Finished media sent to client for approval."
        });
    }

    [HttpPost("{id:guid}/creative-system")]
    public async Task<ActionResult<CreativeSystemResponse>> GenerateCreativeSystem(
        Guid id,
        [FromBody] GenerateCreativeSystemRequest request,
        CancellationToken cancellationToken)
    {
        await GetCurrentCreativeUserAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(request.Prompt))
        {
            throw new InvalidOperationException("Creative prompt is required.");
        }

        var campaign = await _db.Campaigns
            .Include(x => x.User)
                .ThenInclude(x => x.BusinessProfile)
            .Include(x => x.PackageBand)
            .Include(x => x.PackageOrder)
            .Include(x => x.CampaignBrief)
            .Include(x => x.CampaignCreativeSystems)
            .Include(x => x.CampaignRecommendations)
                .ThenInclude(x => x.RecommendationItems)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("Campaign not found.");

        var result = await _creativeStudioIntelligenceService.GenerateAsync(campaign, campaign.CampaignBrief, request, cancellationToken);
        var currentUserId = await _currentUserAccessor.GetCurrentUserIdAsync(cancellationToken);
        var creativeSystem = new CampaignCreativeSystem
        {
            Id = Guid.NewGuid(),
            CampaignId = campaign.Id,
            CreatedByUserId = currentUserId,
            Prompt = request.Prompt.Trim(),
            IterationLabel = string.IsNullOrWhiteSpace(request.IterationLabel) ? null : request.IterationLabel.Trim(),
            InputJson = JsonSerializer.Serialize(request),
            OutputJson = JsonSerializer.Serialize(result),
            CreatedAt = DateTime.UtcNow
        };

        _db.CampaignCreativeSystems.Add(creativeSystem);
        campaign.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        await WriteChangeAuditAsync(
            "generate_creative_system",
            "campaign_creative_system",
            creativeSystem.Id.ToString(),
            ResolveCampaignLabel(campaign),
            $"Generated a creative system for {ResolveCampaignLabel(campaign)}.",
            new
            {
                CampaignId = campaign.Id,
                CreativeSystemId = creativeSystem.Id,
                creativeSystem.IterationLabel
            },
            cancellationToken);

        return Ok(result);
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
        await _changeAuditService.WriteAsync(currentUserId, "creative", action, entityType, entityId, entityLabel, summary, metadata, cancellationToken);
    }

    private async Task<UserAccount> GetCurrentCreativeUserAsync(CancellationToken cancellationToken)
    {
        var currentUserId = await _currentUserAccessor.GetCurrentUserIdAsync(cancellationToken);
        var currentUser = await _db.UserAccounts.FirstOrDefaultAsync(x => x.Id == currentUserId, cancellationToken);
        if (currentUser is null)
        {
            throw new InvalidOperationException("Authenticated user account could not be found.");
        }

        if (currentUser.Role is not UserRole.CreativeDirector and not UserRole.Admin)
        {
            throw new InvalidOperationException("Creative director or admin access is required.");
        }

        return currentUser;
    }

    private static string ResolveCampaignLabel(Advertified.App.Data.Entities.Campaign campaign)
    {
        return string.IsNullOrWhiteSpace(campaign.CampaignName)
            ? $"{campaign.PackageBand?.Name ?? "Campaign"} campaign"
            : campaign.CampaignName.Trim();
    }
}
