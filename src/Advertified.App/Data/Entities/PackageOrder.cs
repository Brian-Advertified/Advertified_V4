using System;
using System.Collections.Generic;

namespace Advertified.App.Data.Entities;

public partial class PackageOrder
{
    public Guid Id { get; set; }

    public Guid? UserId { get; set; }

    public Guid? ProspectLeadId { get; set; }

    public Guid PackageBandId { get; set; }

    public decimal Amount { get; set; }

    public string Currency { get; set; } = null!;

    public string? PaymentProvider { get; set; }

    public string OrderIntent { get; set; } = "sale";

    public string? PaymentReference { get; set; }

    public string PaymentStatus { get; set; } = null!;

    public Guid? SelectedRecommendationId { get; set; }

    public DateTime? SelectedAt { get; set; }

    public string? SelectionSource { get; set; }

    public string SelectionStatus { get; set; } = "none";

    public string? LostReason { get; set; }

    public string? LostStage { get; set; }

    public DateTime? LostAt { get; set; }

    public DateTime? TermsAcceptedAt { get; set; }

    public string? TermsVersion { get; set; }

    public string? TermsAcceptanceSource { get; set; }

    public string CancellationStatus { get; set; } = "none";

    public string? CancellationReason { get; set; }

    public DateTime? CancellationRequestedAt { get; set; }

    public DateTime? PurchasedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public decimal? SelectedBudget { get; set; }

    public decimal AiStudioReservePercent { get; set; }

    public decimal AiStudioReserveAmount { get; set; }

    public string RefundStatus { get; set; } = "none";

    public decimal RefundedAmount { get; set; }

    public decimal GatewayFeeRetainedAmount { get; set; }

    public string? RefundReason { get; set; }

    public DateTime? RefundProcessedAt { get; set; }

    public virtual Campaign? Campaign { get; set; }
    public virtual Invoice? Invoice { get; set; }

    public virtual PackageBand PackageBand { get; set; } = null!;

    public virtual ProspectLead? ProspectLead { get; set; }

    public virtual UserAccount? User { get; set; }
}
