using System;

namespace Advertified.App.Contracts.Admin;

public sealed class AdminIntegrationStatusResponse
{
    public int PaymentRequestAuditCount { get; set; }
    public int PaymentWebhookAuditCount { get; set; }
    public DateTime? LastPaymentRequestAt { get; set; }
    public DateTime? LastPaymentWebhookAt { get; set; }
    public bool ResendSendConfigured { get; set; }
    public bool ResendArchiveFallbackEnabled { get; set; }
    public bool ResendWebhookEnabled { get; set; }
    public bool ResendWebhookSigningSecretConfigured { get; set; }
    public string? ResendWebhookEndpointPath { get; set; }
    public int EmailPendingCount { get; set; }
    public int EmailAcceptedCount { get; set; }
    public int EmailDeliveredCount { get; set; }
    public int EmailFailedCount { get; set; }
    public int EmailArchivedCount { get; set; }
    public DateTime? LastEmailAcceptedAt { get; set; }
    public DateTime? LastEmailDeliveredAt { get; set; }
    public DateTime? LastEmailFailedAt { get; set; }
    public DateTime? LastEmailArchivedAt { get; set; }
    public DateTime? LastEmailWebhookAt { get; set; }
}
