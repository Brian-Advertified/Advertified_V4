using Advertified.App.Data;
using Advertified.App.Data.Entities;
using Advertified.App.Data.Enums;
using Advertified.App.Services.Abstractions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Advertified.App.Controllers;

[ApiController]
[Route("creative/campaigns")]
public sealed class CreativeCampaignsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IChangeAuditService _changeAuditService;

    public CreativeCampaignsController(
        AppDbContext db,
        ICurrentUserAccessor currentUserAccessor,
        IChangeAuditService changeAuditService)
    {
        _db = db;
        _currentUserAccessor = currentUserAccessor;
        _changeAuditService = changeAuditService;
    }

    [HttpPost("{id:guid}/send-finished-media-to-client")]
    public async Task<IActionResult> SendFinishedMediaToClient(Guid id, CancellationToken cancellationToken)
    {
        await GetCurrentCreativeUserAsync(cancellationToken);
        var campaign = await _db.Campaigns
            .Include(x => x.PackageBand)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("Campaign not found.");

        if (!string.Equals(campaign.Status, "approved", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(campaign.Status, "creative_changes_requested", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Campaign is not ready for creative approval handoff.",
                Detail = "Finished media can only be sent to the client after the recommendation has been approved and while creative production or creative revision is active.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        campaign.Status = "creative_sent_to_client_for_approval";
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
