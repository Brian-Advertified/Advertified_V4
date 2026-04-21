using System;
using System.Collections.Generic;

namespace Advertified.App.Data.Entities;

public partial class ProspectLead
{
    public Guid Id { get; set; }

    public string FullName { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string NormalizedEmail { get; set; } = null!;

    public string Phone { get; set; } = null!;

    public string NormalizedPhone { get; set; } = null!;

    public string Source { get; set; } = null!;

    public Guid? ClaimedUserId { get; set; }

    public Guid? OwnerAgentUserId { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual ICollection<Campaign> Campaigns { get; set; } = new List<Campaign>();

    public virtual UserAccount? ClaimedUser { get; set; }

    public virtual UserAccount? OwnerAgentUser { get; set; }

    public virtual ICollection<PackageOrder> PackageOrders { get; set; } = new List<PackageOrder>();
}
