namespace Advertified.App.Contracts.Consent;

public sealed class UpsertConsentPreferenceRequest
{
    public string BrowserId { get; set; } = string.Empty;

    public bool NecessaryCookies { get; set; } = true;

    public bool AnalyticsCookies { get; set; }

    public bool MarketingCookies { get; set; }

    public bool PrivacyAccepted { get; set; }
}
