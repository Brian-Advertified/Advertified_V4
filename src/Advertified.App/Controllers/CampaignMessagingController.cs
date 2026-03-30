using Advertified.App.Configuration;
using Advertified.App.Contracts.Messages;
using Advertified.App.Data;
using Advertified.App.Data.Entities;
using Advertified.App.Data.Enums;
using Advertified.App.Services.Abstractions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Advertified.App.Controllers;

[ApiController]
public sealed class CampaignMessagingController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly ITemplatedEmailService _emailService;
    private readonly FrontendOptions _frontendOptions;
    private readonly ILogger<CampaignMessagingController> _logger;

    public CampaignMessagingController(
        AppDbContext db,
        ICurrentUserAccessor currentUserAccessor,
        ITemplatedEmailService emailService,
        IOptions<FrontendOptions> frontendOptions,
        ILogger<CampaignMessagingController> logger)
    {
        _db = db;
        _currentUserAccessor = currentUserAccessor;
        _emailService = emailService;
        _frontendOptions = frontendOptions.Value;
        _logger = logger;
    }

    [HttpGet("agent/messages")]
    public async Task<ActionResult<IReadOnlyList<CampaignConversationListItemResponse>>> GetAgentInbox(CancellationToken cancellationToken)
    {
        var currentUser = await GetCurrentUserAsync(cancellationToken);
        EnsureAgentAccess(currentUser);

        var campaignsQuery = _db.Campaigns
            .AsNoTracking()
            .AsSplitQuery()
            .Include(x => x.User)
            .Include(x => x.PackageBand)
            .Include(x => x.AssignedAgentUser)
            .Include(x => x.CampaignConversation!)
                .ThenInclude(x => x.Messages)
            .AsQueryable();

        if (currentUser.Role == UserRole.Agent)
        {
            campaignsQuery = campaignsQuery.Where(x => x.AssignedAgentUserId == currentUser.Id);
        }

        var campaigns = await campaignsQuery
            .OrderByDescending(x => x.CampaignConversation != null ? x.CampaignConversation.LastMessageAt : null)
            .ThenByDescending(x => x.UpdatedAt)
            .ToArrayAsync(cancellationToken);

        var response = campaigns
            .Select(campaign =>
            {
                var conversation = campaign.CampaignConversation;
                var lastMessage = conversation?.Messages
                    .OrderByDescending(x => x.CreatedAt)
                    .FirstOrDefault();

                return new CampaignConversationListItemResponse
                {
                    CampaignId = campaign.Id,
                    ConversationId = conversation?.Id,
                    CampaignName = ResolveCampaignName(campaign),
                    CampaignStatus = campaign.Status,
                    ClientName = campaign.User.FullName,
                    ClientEmail = campaign.User.Email,
                    PackageBandName = campaign.PackageBand.Name,
                    AssignedAgentName = campaign.AssignedAgentUser?.FullName,
                    LastMessagePreview = string.IsNullOrWhiteSpace(lastMessage?.Body)
                        ? null
                        : Truncate(lastMessage.Body, 140),
                    LastMessageSenderRole = lastMessage?.SenderRole,
                    LastMessageAt = lastMessage is null ? null : new DateTimeOffset(lastMessage.CreatedAt, TimeSpan.Zero),
                    UnreadCount = conversation?.Messages.Count(x => x.SenderRole == "client" && x.ReadByAgentAt is null) ?? 0,
                    HasMessages = conversation?.Messages.Count > 0
                };
            })
            .ToArray();

        return Ok(response);
    }

    [HttpGet("agent/messages/campaigns/{campaignId:guid}")]
    public async Task<ActionResult<CampaignConversationThreadResponse>> GetAgentThread(Guid campaignId, CancellationToken cancellationToken)
    {
        var currentUser = await GetCurrentUserAsync(cancellationToken);
        EnsureAgentAccess(currentUser);

        var campaign = await LoadCampaignForMessagingAsync(campaignId, cancellationToken)
            ?? throw new InvalidOperationException("Campaign not found.");

        EnsureAgentCampaignAccess(currentUser, campaign);
        await MarkMessagesAsReadAsync(campaign.CampaignConversation, "agent", cancellationToken);

        return Ok(BuildThreadResponse(campaign, "agent"));
    }

    [HttpPost("agent/messages/campaigns/{campaignId:guid}")]
    public async Task<ActionResult<CampaignConversationThreadResponse>> SendAgentMessage(Guid campaignId, [FromBody] SendCampaignMessageRequest request, CancellationToken cancellationToken)
    {
        var currentUser = await GetCurrentUserAsync(cancellationToken);
        EnsureAgentAccess(currentUser);

        if (string.IsNullOrWhiteSpace(request.Body))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Message body is required.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        var campaign = await LoadCampaignForMessagingAsync(campaignId, cancellationToken)
            ?? throw new InvalidOperationException("Campaign not found.");

        EnsureAgentCampaignAccess(currentUser, campaign);
        var conversation = EnsureConversation(campaign);
        var now = DateTime.UtcNow;
        conversation.UpdatedAt = now;
        conversation.LastMessageAt = now;

        var message = new CampaignMessage
        {
            Id = Guid.NewGuid(),
            ConversationId = conversation.Id,
            SenderUserId = currentUser.Id,
            SenderRole = "agent",
            Body = request.Body.Trim(),
            CreatedAt = now,
            ReadByAgentAt = now
        };

        conversation.Messages.Add(message);
        campaign.UpdatedAt = now;
        await _db.SaveChangesAsync(cancellationToken);

        await SendMessageNotificationAsync(
            campaign,
            recipientEmail: campaign.User.Email,
            recipientName: campaign.User.FullName,
            senderName: currentUser.FullName,
            senderRole: "Agent",
            threadUrl: BuildFrontendUrl($"/campaigns/{campaign.Id}/messages"),
            messagePreview: message.Body,
            cancellationToken);

        return Ok(BuildThreadResponse(campaign, "agent"));
    }

    [HttpGet("campaigns/{campaignId:guid}/messages")]
    public async Task<ActionResult<CampaignConversationThreadResponse>> GetClientThread(Guid campaignId, CancellationToken cancellationToken)
    {
        var currentUser = await GetCurrentUserAsync(cancellationToken);

        var campaign = await LoadCampaignForMessagingAsync(campaignId, cancellationToken)
            ?? throw new InvalidOperationException("Campaign not found.");

        EnsureClientAccess(currentUser, campaign);
        await MarkMessagesAsReadAsync(campaign.CampaignConversation, "client", cancellationToken);

        return Ok(BuildThreadResponse(campaign, "client"));
    }

    [HttpPost("campaigns/{campaignId:guid}/messages")]
    public async Task<ActionResult<CampaignConversationThreadResponse>> SendClientMessage(Guid campaignId, [FromBody] SendCampaignMessageRequest request, CancellationToken cancellationToken)
    {
        var currentUser = await GetCurrentUserAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(request.Body))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Message body is required.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        var campaign = await LoadCampaignForMessagingAsync(campaignId, cancellationToken)
            ?? throw new InvalidOperationException("Campaign not found.");

        EnsureClientAccess(currentUser, campaign);

        var conversation = EnsureConversation(campaign);
        var now = DateTime.UtcNow;
        conversation.UpdatedAt = now;
        conversation.LastMessageAt = now;

        var message = new CampaignMessage
        {
            Id = Guid.NewGuid(),
            ConversationId = conversation.Id,
            SenderUserId = currentUser.Id,
            SenderRole = "client",
            Body = request.Body.Trim(),
            CreatedAt = now,
            ReadByClientAt = now
        };

        conversation.Messages.Add(message);
        campaign.UpdatedAt = now;
        await _db.SaveChangesAsync(cancellationToken);

        var agentRecipient = campaign.AssignedAgentUser;
        if (agentRecipient is not null)
        {
            await SendMessageNotificationAsync(
                campaign,
                recipientEmail: agentRecipient.Email,
                recipientName: agentRecipient.FullName,
                senderName: currentUser.FullName,
                senderRole: "Client",
                threadUrl: BuildFrontendUrl($"/agent/messages?campaignId={campaign.Id}"),
                messagePreview: message.Body,
                cancellationToken);
        }

        return Ok(BuildThreadResponse(campaign, "client"));
    }

    private async Task<UserAccount> GetCurrentUserAsync(CancellationToken cancellationToken)
    {
        var currentUserId = await _currentUserAccessor.GetCurrentUserIdAsync(cancellationToken);
        return await _db.UserAccounts.FirstAsync(x => x.Id == currentUserId, cancellationToken);
    }

    private async Task<Campaign?> LoadCampaignForMessagingAsync(Guid campaignId, CancellationToken cancellationToken)
    {
        return await _db.Campaigns
            .Include(x => x.User)
            .Include(x => x.PackageBand)
            .Include(x => x.AssignedAgentUser)
            .Include(x => x.CampaignConversation!)
                .ThenInclude(x => x.Messages)
                    .ThenInclude(x => x.SenderUser)
            .FirstOrDefaultAsync(x => x.Id == campaignId, cancellationToken);
    }

    private static void EnsureAgentAccess(UserAccount user)
    {
        if (user.Role is not UserRole.Agent and not UserRole.Admin)
        {
            throw new InvalidOperationException("Agent access is required.");
        }
    }

    private static void EnsureClientAccess(UserAccount user, Campaign campaign)
    {
        if (user.Role == UserRole.Admin)
        {
            return;
        }

        if (campaign.UserId != user.Id)
        {
            throw new InvalidOperationException("Campaign not found.");
        }
    }

    private static void EnsureAgentCampaignAccess(UserAccount user, Campaign campaign)
    {
        if (user.Role == UserRole.Admin)
        {
            return;
        }

        if (campaign.AssignedAgentUserId != user.Id)
        {
            throw new InvalidOperationException("Campaign not found.");
        }
    }

    private CampaignConversation EnsureConversation(Campaign campaign)
    {
        if (campaign.CampaignConversation is not null)
        {
            return campaign.CampaignConversation;
        }

        var now = DateTime.UtcNow;
        var conversation = new CampaignConversation
        {
            Id = Guid.NewGuid(),
            CampaignId = campaign.Id,
            ClientUserId = campaign.UserId,
            CreatedAt = now,
            UpdatedAt = now,
            Campaign = campaign,
            ClientUser = campaign.User
        };

        campaign.CampaignConversation = conversation;
        _db.CampaignConversations.Add(conversation);
        return conversation;
    }

    private async Task MarkMessagesAsReadAsync(CampaignConversation? conversation, string viewerRole, CancellationToken cancellationToken)
    {
        if (conversation is null)
        {
            return;
        }

        var now = DateTime.UtcNow;
        var changed = false;
        foreach (var message in conversation.Messages)
        {
            if (viewerRole == "agent" && message.SenderRole == "client" && message.ReadByAgentAt is null)
            {
                message.ReadByAgentAt = now;
                changed = true;
            }
            else if (viewerRole == "client" && message.SenderRole == "agent" && message.ReadByClientAt is null)
            {
                message.ReadByClientAt = now;
                changed = true;
            }
        }

        if (changed)
        {
            conversation.UpdatedAt = now;
            await _db.SaveChangesAsync(cancellationToken);
        }
    }

    private static CampaignConversationThreadResponse BuildThreadResponse(Campaign campaign, string viewerRole)
    {
        var conversation = campaign.CampaignConversation;
        var messages = conversation?.Messages
            .OrderBy(x => x.CreatedAt)
            .Select(message => new CampaignConversationMessageResponse
            {
                Id = message.Id,
                SenderUserId = message.SenderUserId,
                SenderRole = message.SenderRole,
                SenderName = message.SenderUser?.FullName ?? "Unknown sender",
                Body = message.Body,
                CreatedAt = new DateTimeOffset(message.CreatedAt, TimeSpan.Zero),
                IsRead = viewerRole == "agent"
                    ? message.ReadByAgentAt is not null
                    : message.ReadByClientAt is not null
            })
            .ToArray()
            ?? Array.Empty<CampaignConversationMessageResponse>();

        return new CampaignConversationThreadResponse
        {
            CampaignId = campaign.Id,
            ConversationId = conversation?.Id,
            CampaignName = ResolveCampaignName(campaign),
            CampaignStatus = campaign.Status,
            ClientName = campaign.User.FullName,
            ClientEmail = campaign.User.Email,
            PackageBandName = campaign.PackageBand.Name,
            AssignedAgentName = campaign.AssignedAgentUser?.FullName,
            UnreadCount = viewerRole == "agent"
                ? conversation?.Messages.Count(x => x.SenderRole == "client" && x.ReadByAgentAt is null) ?? 0
                : conversation?.Messages.Count(x => x.SenderRole == "agent" && x.ReadByClientAt is null) ?? 0,
            CanSend = true,
            Messages = messages
        };
    }

    private async Task SendMessageNotificationAsync(
        Campaign campaign,
        string recipientEmail,
        string recipientName,
        string senderName,
        string senderRole,
        string threadUrl,
        string messagePreview,
        CancellationToken cancellationToken)
    {
        try
        {
            await _emailService.SendAsync(
                "campaign-message-notification",
                recipientEmail,
                "campaigns",
                new Dictionary<string, string?>
                {
                    ["RecipientName"] = recipientName,
                    ["SenderName"] = senderName,
                    ["SenderRole"] = senderRole,
                    ["CampaignName"] = ResolveCampaignName(campaign),
                    ["PackageName"] = campaign.PackageBand.Name,
                    ["ThreadUrl"] = threadUrl,
                    ["MessagePreview"] = Truncate(messagePreview, 240)
                },
                null,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send campaign message notification for campaign {CampaignId}.", campaign.Id);
        }
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

    private static string Truncate(string value, int maxLength)
    {
        if (value.Length <= maxLength)
        {
            return value;
        }

        return value[..Math.Max(0, maxLength - 3)].TrimEnd() + "...";
    }
}
