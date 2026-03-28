using System;
using System.Collections.Generic;

namespace Advertified.App.Data.Entities;

public partial class PackageBand
{
    public Guid Id { get; set; }

    public string Code { get; set; } = null!;

    public string Name { get; set; } = null!;

    public decimal MinBudget { get; set; }

    public decimal MaxBudget { get; set; }

    public int SortOrder { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual ICollection<Campaign> Campaigns { get; set; } = new List<Campaign>();

    public virtual ICollection<PackageOrder> PackageOrders { get; set; } = new List<PackageOrder>();
}
