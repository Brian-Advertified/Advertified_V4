using System;
using System.Collections.Generic;

namespace Advertified.App.Data.Entities;

public partial class PackageOrder
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public Guid PackageBandId { get; set; }

    public decimal Amount { get; set; }

    public string Currency { get; set; } = null!;

    public string? PaymentProvider { get; set; }

    public string? PaymentReference { get; set; }

    public string PaymentStatus { get; set; } = null!;

    public DateTime? PurchasedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public decimal? SelectedBudget { get; set; }

    public virtual Campaign? Campaign { get; set; }
    public virtual Invoice? Invoice { get; set; }

    public virtual PackageBand PackageBand { get; set; } = null!;

    public virtual UserAccount User { get; set; } = null!;
}
