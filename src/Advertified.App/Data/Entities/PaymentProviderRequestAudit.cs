namespace Advertified.App.Data.Entities;

public sealed class PaymentProviderRequestAudit
{
    public Guid Id { get; set; }

    public Guid? PackageOrderId { get; set; }

    public string Provider { get; set; } = string.Empty;

    public string EventType { get; set; } = string.Empty;

    public string? ExternalReference { get; set; }

    public string RequestUrl { get; set; } = string.Empty;

    public string RequestHeadersJson { get; set; } = "{}";

    public string RequestBodyJson { get; set; } = string.Empty;

    public int? ResponseStatusCode { get; set; }

    public string? ResponseHeadersJson { get; set; }

    public string? ResponseBodyText { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public PackageOrder? PackageOrder { get; set; }
}
