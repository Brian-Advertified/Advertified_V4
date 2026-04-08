using System;

namespace Advertified.App.Data.Entities;

public partial class RecommendationRunAudit
{
    public Guid Id { get; set; }

    public Guid CampaignId { get; set; }

    public Guid RecommendationId { get; set; }

    public string RecommendationType { get; set; } = null!;

    public int RevisionNumber { get; set; }

    public string? RequestSnapshotJson { get; set; }

    public string? PolicySnapshotJson { get; set; }

    public string? InventorySnapshotJson { get; set; }

    public string? CandidateCountsJson { get; set; }

    public string? RejectedCandidatesJson { get; set; }

    public string? SelectedItemsJson { get; set; }

    public string? FallbackFlagsJson { get; set; }

    public decimal BudgetUtilizationRatio { get; set; }

    public bool ManualReviewRequired { get; set; }

    public string? FinalRationale { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Campaign Campaign { get; set; } = null!;

    public virtual CampaignRecommendation Recommendation { get; set; } = null!;
}
