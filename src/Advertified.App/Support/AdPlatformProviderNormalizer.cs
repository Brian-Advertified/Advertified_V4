namespace Advertified.App.Support;

public static class AdPlatformProviderNormalizer
{
    public static string Normalize(string? value, string fallback = "meta")
    {
        var normalized = (value ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return fallback;
        }

        return normalized switch
        {
            "google" => "googleads",
            "google ads" => "googleads",
            "google_ads" => "googleads",
            "googleads" => "googleads",
            "meta" => "meta",
            "facebook" => "meta",
            "facebook ads" => "meta",
            "facebookads" => "meta",
            _ => normalized
        };
    }
}
