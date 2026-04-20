namespace Advertified.App.Data.Entities;

public sealed class EmailDeliveryMessage
{
    public Guid Id { get; set; }
    public string ProviderKey { get; set; } = string.Empty;
    public string TemplateName { get; set; } = string.Empty;
    public string SenderKey { get; set; } = string.Empty;
    public string DeliveryPurpose { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string FromAddress { get; set; } = string.Empty;
    public string RecipientEmail { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string BodyHtml { get; set; } = string.Empty;
    public string? AttachmentsJson { get; set; }
    public Guid? CampaignId { get; set; }
    public Guid? RecommendationId { get; set; }
    public int? RecommendationRevisionNumber { get; set; }
    public Guid? RecipientUserId { get; set; }
    public Guid? ProspectLeadId { get; set; }
    public string? ProviderMessageId { get; set; }
    public string? ProviderBroadcastId { get; set; }
    public string? LatestEventType { get; set; }
    public DateTime? LatestEventAt { get; set; }
    public DateTime? AcceptedAt { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public DateTime? OpenedAt { get; set; }
    public DateTime? ClickedAt { get; set; }
    public DateTime? ComplainedAt { get; set; }
    public DateTime? BouncedAt { get; set; }
    public DateTime? FailedAt { get; set; }
    public DateTime? ArchivedAt { get; set; }
    public string? ArchivedPath { get; set; }
    public string? LastError { get; set; }
    public string? MetadataJson { get; set; }
    public int AttemptCount { get; set; }
    public DateTime? LastAttemptAt { get; set; }
    public DateTime? NextAttemptAt { get; set; }
    public DateTime? LockedAt { get; set; }
    public Guid? LockToken { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Campaign? Campaign { get; set; }
    public CampaignRecommendation? Recommendation { get; set; }
    public UserAccount? RecipientUser { get; set; }
    public ProspectLead? ProspectLead { get; set; }
    public ICollection<EmailDeliveryEvent> Events { get; set; } = new List<EmailDeliveryEvent>();
}
