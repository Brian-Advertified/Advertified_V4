using System;
using System.Collections.Generic;

namespace Advertified.App.Data.Entities;

public partial class BusinessProfile
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public string BusinessName { get; set; } = null!;

    public string BusinessType { get; set; } = null!;

    public string RegistrationNumber { get; set; } = null!;

    public string? VatNumber { get; set; }

    public string Industry { get; set; } = null!;

    public string AnnualRevenueBand { get; set; } = null!;

    public string? TradingAsName { get; set; }

    public string StreetAddress { get; set; } = null!;

    public string City { get; set; } = null!;

    public string Province { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual UserAccount User { get; set; } = null!;
}
