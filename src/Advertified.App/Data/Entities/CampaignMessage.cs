using System;

namespace Advertified.App.Data.Entities;

public class CampaignMessage
{
    public Guid Id { get; set; }

    public Guid ConversationId { get; set; }

    public Guid SenderUserId { get; set; }

    public string SenderRole { get; set; } = null!;

    public string Body { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime? ReadByClientAt { get; set; }

    public DateTime? ReadByAgentAt { get; set; }

    public DateTime? EmailNotificationSentAt { get; set; }

    public virtual CampaignConversation Conversation { get; set; } = null!;

    public virtual UserAccount SenderUser { get; set; } = null!;
}
