using System;
using System.Collections.Generic;

namespace Advertified.App.Data.Entities;

public partial class RecommendationItem
{
    public Guid Id { get; set; }

    public Guid RecommendationId { get; set; }

    public string InventoryType { get; set; } = null!;

    public Guid? InventoryItemId { get; set; }

    public string DisplayName { get; set; } = null!;

    public int Quantity { get; set; }

    public decimal UnitCost { get; set; }

    public decimal TotalCost { get; set; }

    public string? MetadataJson { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual CampaignRecommendation Recommendation { get; set; } = null!;
}
