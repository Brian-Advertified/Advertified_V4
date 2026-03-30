namespace Advertified.App.Contracts.Messages;

public sealed class CampaignConversationListItemResponse
{
    public Guid CampaignId { get; set; }
    public Guid? ConversationId { get; set; }
    public string CampaignName { get; set; } = string.Empty;
    public string CampaignStatus { get; set; } = string.Empty;
    public string ClientName { get; set; } = string.Empty;
    public string ClientEmail { get; set; } = string.Empty;
    public string PackageBandName { get; set; } = string.Empty;
    public string? AssignedAgentName { get; set; }
    public string? LastMessagePreview { get; set; }
    public string? LastMessageSenderRole { get; set; }
    public DateTimeOffset? LastMessageAt { get; set; }
    public int UnreadCount { get; set; }
    public bool HasMessages { get; set; }
}
