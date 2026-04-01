using System;
using System.Collections.Generic;

namespace Advertified.App.Data.Entities;

public partial class CampaignCreativeSystem
{
    public Guid Id { get; set; }

    public Guid CampaignId { get; set; }

    public Guid? CreatedByUserId { get; set; }

    public string Prompt { get; set; } = null!;

    public string? IterationLabel { get; set; }

    public string InputJson { get; set; } = null!;

    public string OutputJson { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public virtual Campaign Campaign { get; set; } = null!;

    public virtual UserAccount? CreatedByUser { get; set; }

    public virtual ICollection<CampaignCreative> CampaignCreatives { get; set; } = new List<CampaignCreative>();
}
