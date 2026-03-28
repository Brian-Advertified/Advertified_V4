using System;
using System.Collections.Generic;

namespace Advertified.App.Data.Entities;

public partial class CampaignBriefDraft
{
    public Guid Id { get; set; }

    public Guid CampaignId { get; set; }

    public string DraftJson { get; set; } = null!;

    public DateTime SavedAt { get; set; }

    public virtual Campaign Campaign { get; set; } = null!;
}
