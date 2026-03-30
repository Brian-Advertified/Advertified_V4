namespace Advertified.App.Contracts.Messages;

public sealed class CampaignConversationThreadResponse
{
    public Guid CampaignId { get; set; }
    public Guid? ConversationId { get; set; }
    public string CampaignName { get; set; } = string.Empty;
    public string CampaignStatus { get; set; } = string.Empty;
    public string ClientName { get; set; } = string.Empty;
    public string ClientEmail { get; set; } = string.Empty;
    public string PackageBandName { get; set; } = string.Empty;
    public string? AssignedAgentName { get; set; }
    public int UnreadCount { get; set; }
    public bool CanSend { get; set; }
    public IReadOnlyList<CampaignConversationMessageResponse> Messages { get; set; } = Array.Empty<CampaignConversationMessageResponse>();
}

public sealed class CampaignConversationMessageResponse
{
    public Guid Id { get; set; }
    public Guid SenderUserId { get; set; }
    public string SenderRole { get; set; } = string.Empty;
    public string SenderName { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public bool IsRead { get; set; }
}
