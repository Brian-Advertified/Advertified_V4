using System;

namespace Advertified.App.Data.Entities;

public class ConsentPreference
{
    public Guid Id { get; set; }

    public Guid? UserId { get; set; }

    public string BrowserId { get; set; } = null!;

    public bool NecessaryCookies { get; set; }

    public bool AnalyticsCookies { get; set; }

    public bool MarketingCookies { get; set; }

    public bool PrivacyAccepted { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual UserAccount? User { get; set; }
}
