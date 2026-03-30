using System;

namespace Advertified.App.Contracts.Admin;

public sealed class AdminAuditEntryResponse
{
    public Guid Id { get; set; }
    public string Source { get; set; } = string.Empty;
    public string ActorName { get; set; } = string.Empty;
    public string ActorRole { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string? EntityType { get; set; }
    public string? EntityLabel { get; set; }
    public string Context { get; set; } = string.Empty;
    public string? StatusLabel { get; set; }
    public DateTime CreatedAt { get; set; }
}
