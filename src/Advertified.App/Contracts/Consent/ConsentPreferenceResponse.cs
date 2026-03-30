namespace Advertified.App.Contracts.Consent;

public sealed class ConsentPreferenceResponse
{
    public string BrowserId { get; set; } = string.Empty;

    public bool NecessaryCookies { get; set; }

    public bool AnalyticsCookies { get; set; }

    public bool MarketingCookies { get; set; }

    public bool PrivacyAccepted { get; set; }

    public bool HasSavedPreferences { get; set; }
}
