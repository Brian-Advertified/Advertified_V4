using System;

namespace Advertified.App.Data.Entities;

public class LeadInsight
{
    public int Id { get; set; }

    public int LeadId { get; set; }

    public int? SignalId { get; set; }

    public string TrendSummary { get; set; } = string.Empty;

    public int ScoreSnapshot { get; set; }

    public string IntentLevelSnapshot { get; set; } = string.Empty;

    public string Text { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public virtual ICollection<LeadAction> Actions { get; set; } = new List<LeadAction>();

    public virtual Lead Lead { get; set; } = null!;

    public virtual Signal? Signal { get; set; }
}
