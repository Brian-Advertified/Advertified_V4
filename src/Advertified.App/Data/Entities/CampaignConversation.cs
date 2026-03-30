using System;
using System.Collections.Generic;

namespace Advertified.App.Data.Entities;

public class CampaignConversation
{
    public Guid Id { get; set; }

    public Guid CampaignId { get; set; }

    public Guid ClientUserId { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public DateTime? LastMessageAt { get; set; }

    public virtual Campaign Campaign { get; set; } = null!;

    public virtual UserAccount ClientUser { get; set; } = null!;

    public virtual ICollection<CampaignMessage> Messages { get; set; } = new List<CampaignMessage>();
}
