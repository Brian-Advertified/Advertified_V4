using System;

namespace Advertified.App.Data.Entities;

public class Signal
{
    public int Id { get; set; }

    public int LeadId { get; set; }

    public bool HasPromo { get; set; }

    public bool HasMetaAds { get; set; }

    public bool WebsiteUpdatedRecently { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual ICollection<LeadInsight> Insights { get; set; } = new List<LeadInsight>();

    public virtual Lead Lead { get; set; } = null!;
}
