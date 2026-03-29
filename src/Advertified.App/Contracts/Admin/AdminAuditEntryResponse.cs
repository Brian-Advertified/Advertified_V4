using System;

namespace Advertified.App.Contracts.Admin;

public sealed class AdminAuditEntryResponse
{
    public Guid Id { get; set; }
    public string Source { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string? ExternalReference { get; set; }
    public string RequestUrl { get; set; } = string.Empty;
    public int? ResponseStatusCode { get; set; }
    public DateTime CreatedAt { get; set; }
}
