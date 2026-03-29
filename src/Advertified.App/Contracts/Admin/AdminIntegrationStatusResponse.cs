using System;

namespace Advertified.App.Contracts.Admin;

public sealed class AdminIntegrationStatusResponse
{
    public int PaymentRequestAuditCount { get; set; }
    public int PaymentWebhookAuditCount { get; set; }
    public DateTime? LastPaymentRequestAt { get; set; }
    public DateTime? LastPaymentWebhookAt { get; set; }
}
