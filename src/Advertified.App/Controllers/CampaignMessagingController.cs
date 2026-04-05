using Advertified.App.Support;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Advertified.App.Configuration;
using Advertified.App.Contracts.Messages;
using Advertified.App.Data;
using Advertified.App.Data.Entities;
using Advertified.App.Data.Enums;
using Advertified.App.Services.Abstractions;
using Microsoft.AspNetCore.Authorization;

namespace Advertified.App.Controllers;

[ApiController]
[Authorize]
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
            .AsQueryable();

        if (currentUser.Role == UserRole.Agent)
        {
            campaignsQuery = campaignsQuery.Where(x => x.AssignedAgentUserId == currentUser.Id);
        }

        var response = await campaignsQuery
            .OrderByDescending(x => x.CampaignConversation != null ? x.CampaignConversation.LastMessageAt : null)
            .ThenByDescending(x => x.UpdatedAt)
            .Select(campaign => new AgentMessageInboxProjection
            {
                CampaignId = campaign.Id,
                ConversationId = campaign.CampaignConversation != null ? campaign.CampaignConversation.Id : null,
                CampaignName = string.IsNullOrWhiteSpace(campaign.CampaignName)
                    ? $"{campaign.PackageBand.Name} campaign"
                    : campaign.CampaignName.Trim(),
                CampaignStatus = campaign.Status,
                ClientName = campaign.User.FullName,
                ClientEmail = campaign.User.Email,
                PackageBandName = campaign.PackageBand.Name,
                AssignedAgentName = campaign.AssignedAgentUser != null ? campaign.AssignedAgentUser.FullName : null,
                LatestMessageBody = campaign.CampaignConversation != null
                    ? campaign.CampaignConversation.Messages
                        .OrderByDescending(message => message.CreatedAt)
                        .Select(message => message.Body)
                        .FirstOrDefault()
                    : null,
                LatestMessageSenderRole = campaign.CampaignConversation != null
                    ? campaign.CampaignConversation.Messages
                        .OrderByDescending(message => message.CreatedAt)
                        .Select(message => message.SenderRole)
                        .FirstOrDefault()
                    : null,
                LatestMessageCreatedAt = campaign.CampaignConversation != null
                    ? campaign.CampaignConversation.Messages
                        .OrderByDescending(message => message.CreatedAt)
                        .Select(message => (DateTime?)message.CreatedAt)
                        .FirstOrDefault()
                    : null,
                UnreadCount = campaign.CampaignConversation != null
                    ? campaign.CampaignConversation.Messages.Count(message => message.SenderRole == ConversationParticipantRoles.Client && message.ReadByAgentAt == null)
                    : 0,
                HasMessages = campaign.CampaignConversation != null && campaign.CampaignConversation.Messages.Any()
            })
            .ToArrayAsync(cancellationToken);

        return Ok(response.Select(item => item.ToResponse()).ToArray());
    }

    [HttpGet("agent/messages/campaigns/{campaignId:guid}")]
    public async Task<ActionResult<CampaignConversationThreadResponse>> GetAgentThread(Guid campaignId, CancellationToken cancellationToken)
    {
        var currentUser = await GetCurrentUserAsync(cancellationToken);
        EnsureAgentAccess(currentUser);

        var campaign = await LoadCampaignForMessagingAsync(campaignId, cancellationToken)
            ?? throw new InvalidOperationException("Campaign not found.");

        EnsureAgentCampaignAccess(currentUser, campaign);
        await MarkMessagesAsReadAsync(campaign.CampaignConversation?.Id, ConversationParticipantRoles.Agent, cancellationToken);

        return Ok(BuildThreadResponse(campaign, ConversationParticipantRoles.Agent));
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
            SenderRole = ConversationParticipantRoles.Agent,
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

        return Ok(BuildThreadResponse(campaign, ConversationParticipantRoles.Agent));
    }

    [HttpGet("campaigns/{campaignId:guid}/messages")]
    public async Task<ActionResult<CampaignConversationThreadResponse>> GetClientThread(Guid campaignId, CancellationToken cancellationToken)
    {
        var currentUser = await GetCurrentUserAsync(cancellationToken);

        var campaign = await LoadCampaignForMessagingAsync(campaignId, cancellationToken)
            ?? throw new InvalidOperationException("Campaign not found.");

        EnsureClientAccess(currentUser, campaign);
        await MarkMessagesAsReadAsync(campaign.CampaignConversation?.Id, ConversationParticipantRoles.Client, cancellationToken);

        return Ok(BuildThreadResponse(campaign, ConversationParticipantRoles.Client));
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
            SenderRole = ConversationParticipantRoles.Client,
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

        return Ok(BuildThreadResponse(campaign, ConversationParticipantRoles.Client));
    }

    private async Task<UserAccount> GetCurrentUserAsync(CancellationToken cancellationToken)
    {
        var currentUserId = await _currentUserAccessor.GetCurrentUserIdAsync(cancellationToken);
        return await _db.UserAccounts.FirstAsync(x => x.Id == currentUserId, cancellationToken);
    }

    private sealed class AgentMessageInboxProjection
    {
        public Guid CampaignId { get; init; }
        public Guid? ConversationId { get; init; }
        public string CampaignName { get; init; } = string.Empty;
        public string CampaignStatus { get; init; } = string.Empty;
        public string ClientName { get; init; } = string.Empty;
        public string ClientEmail { get; init; } = string.Empty;
        public string PackageBandName { get; init; } = string.Empty;
        public string? AssignedAgentName { get; init; }
        public string? LatestMessageBody { get; init; }
        public string? LatestMessageSenderRole { get; init; }
        public DateTime? LatestMessageCreatedAt { get; init; }
        public int UnreadCount { get; init; }
        public bool HasMessages { get; init; }

        public CampaignConversationListItemResponse ToResponse()
        {
            return new CampaignConversationListItemResponse
            {
                CampaignId = CampaignId,
                ConversationId = ConversationId,
                CampaignName = CampaignName,
                CampaignStatus = CampaignStatus,
                ClientName = ClientName,
                ClientEmail = ClientEmail,
                PackageBandName = PackageBandName,
                AssignedAgentName = AssignedAgentName,
                LastMessagePreview = string.IsNullOrWhiteSpace(LatestMessageBody) ? null : Truncate(LatestMessageBody, 140),
                LastMessageSenderRole = LatestMessageSenderRole,
                LastMessageAt = LatestMessageCreatedAt.HasValue ? new DateTimeOffset(LatestMessageCreatedAt.Value, TimeSpan.Zero) : null,
                UnreadCount = UnreadCount,
                HasMessages = HasMessages
            };
        }
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
            throw new ForbiddenException("Agent access is required.");
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

    private async Task MarkMessagesAsReadAsync(Guid? conversationId, string viewerRole, CancellationToken cancellationToken)
    {
        if (conversationId is null)
        {
            return;
        }

        var now = DateTime.UtcNow;
        if (viewerRole == ConversationParticipantRoles.Agent)
        {
            await _db.CampaignMessages
                .Where(message => message.ConversationId == conversationId.Value && message.SenderRole == ConversationParticipantRoles.Client && message.ReadByAgentAt == null)
                .ExecuteUpdateAsync(setters => setters.SetProperty(message => message.ReadByAgentAt, now), cancellationToken);
        }
        else if (viewerRole == ConversationParticipantRoles.Client)
        {
            await _db.CampaignMessages
                .Where(message => message.ConversationId == conversationId.Value && message.SenderRole == ConversationParticipantRoles.Agent && message.ReadByClientAt == null)
                .ExecuteUpdateAsync(setters => setters.SetProperty(message => message.ReadByClientAt, now), cancellationToken);
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
                IsRead = viewerRole == ConversationParticipantRoles.Agent
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
            UnreadCount = viewerRole == ConversationParticipantRoles.Agent
                ? conversation?.Messages.Count(x => x.SenderRole == ConversationParticipantRoles.Client && x.ReadByAgentAt is null) ?? 0
                : conversation?.Messages.Count(x => x.SenderRole == ConversationParticipantRoles.Agent && x.ReadByClientAt is null) ?? 0,
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
