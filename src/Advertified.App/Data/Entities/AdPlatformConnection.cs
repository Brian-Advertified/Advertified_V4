namespace Advertified.App.Data.Entities;

public sealed class AdPlatformConnection
{
    public Guid Id { get; set; }
    public Guid? OwnerUserId { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string ExternalAccountId { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public string Status { get; set; } = "active";
    public string? AccessToken { get; set; }
    public string? RefreshToken { get; set; }
    public DateTime? TokenExpiresAt { get; set; }
    public string? MetadataJson { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? LastSyncedAt { get; set; }
}

