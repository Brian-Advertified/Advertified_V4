using System;
using System.Collections.Generic;

namespace Advertified.App.Data.Entities;

public partial class CampaignRecommendation
{
    public Guid Id { get; set; }

    public Guid CampaignId { get; set; }

    public string RecommendationType { get; set; } = null!;

    public string GeneratedBy { get; set; } = null!;

    public string Status { get; set; } = null!;

    public decimal TotalCost { get; set; }

    public decimal EstimatedSupplierCost { get; set; }

    public decimal EstimatedGrossProfit { get; set; }

    public decimal? EstimatedGrossMarginPercent { get; set; }

    public string MarginStatus { get; set; } = "unchecked";

    public string? ClientExplanation { get; set; }

    public string SupplierAvailabilityStatus { get; set; } = "unconfirmed";

    public DateTime? SupplierAvailabilityCheckedAt { get; set; }

    public string? SupplierAvailabilityNotes { get; set; }

    public string? Summary { get; set; }

    public string? Rationale { get; set; }

    public Guid? CreatedByUserId { get; set; }

    public string? RequestSnapshotJson { get; set; }

    public string? PolicySnapshotJson { get; set; }

    public string? InventorySnapshotJson { get; set; }

    public string? InventoryBatchRefsJson { get; set; }

    public int RevisionNumber { get; set; }

    public DateTime? SentToClientAt { get; set; }

    public DateTime? ApprovedAt { get; set; }

    public string? PdfStorageObjectKey { get; set; }

    public DateTime? PdfGeneratedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual Campaign Campaign { get; set; } = null!;

    public virtual UserAccount? CreatedByUser { get; set; }

    public virtual ICollection<RecommendationItem> RecommendationItems { get; set; } = new List<RecommendationItem>();

    public virtual ICollection<RecommendationRunAudit> RecommendationRunAudits { get; set; } = new List<RecommendationRunAudit>();
}
