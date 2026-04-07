using System.Text.RegularExpressions;
using Advertified.App.Services.Abstractions;

namespace Advertified.App.Services;

public sealed partial class WebsiteSignalProvider : IWebsiteSignalProvider
{
    private static readonly string[] PromoKeywords = new[]
    {
        "sale",
        "promo",
        "offer",
        "special",
        "discount",
        "clearance",
        "limited time"
    };
    private static readonly string[] PromoPaths = new[]
    {
        "/promo",
        "/campaign",
        "/offer",
        "/offers",
        "/deal",
        "/deals",
        "/special",
        "/specials",
        "/sale"
    };
    private static readonly string[] MetaAdMarkers = new[]
    {
        "connect.facebook.net",
        "facebook pixel",
        "fbq(",
        "meta pixel",
        "www.facebook.com/tr",
        "instagram.com"
    };

    private readonly HttpClient _httpClient;

    public WebsiteSignalProvider(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<WebsiteSignalResult> CollectAsync(string? websiteUrl, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(websiteUrl))
        {
            return new WebsiteSignalResult();
        }

        var normalizedUrl = NormalizeWebsiteUrl(websiteUrl);
        if (!Uri.TryCreate(normalizedUrl, UriKind.Absolute, out var websiteUri))
        {
            return new WebsiteSignalResult();
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, websiteUri);
            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return BuildResult(string.Empty, response.Headers.Date?.UtcDateTime, null);
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var lastModified = response.Content.Headers.LastModified?.UtcDateTime
                ?? response.Headers.Date?.UtcDateTime;

            return BuildResult(content, lastModified, websiteUri);
        }
        catch (HttpRequestException)
        {
            return new WebsiteSignalResult();
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new WebsiteSignalResult();
        }
    }

    private static WebsiteSignalResult BuildResult(string content, DateTime? lastModifiedUtc, Uri? websiteUri)
    {
        var lowered = content.ToLowerInvariant();
        var hasPromoKeyword = PromoKeywords.Any(keyword => lowered.Contains(keyword, StringComparison.OrdinalIgnoreCase));
        var hasPromoPath = PromoPaths.Any(path =>
            lowered.Contains($"href=\"{path}", StringComparison.OrdinalIgnoreCase)
            || lowered.Contains($"href='{path}", StringComparison.OrdinalIgnoreCase)
            || lowered.Contains(path, StringComparison.OrdinalIgnoreCase));
        var hasMetaAds = MetaAdMarkers.Any(marker => lowered.Contains(marker, StringComparison.OrdinalIgnoreCase))
            || MetaPixelRegex().IsMatch(content);
        var updatedRecently = lastModifiedUtc.HasValue && lastModifiedUtc.Value >= DateTime.UtcNow.AddDays(-30);

        if (!hasPromoPath && websiteUri is not null)
        {
            hasPromoPath = PromoPaths.Any(path => websiteUri.AbsolutePath.Contains(path, StringComparison.OrdinalIgnoreCase));
        }

        return new WebsiteSignalResult
        {
            HasPromo = hasPromoKeyword || hasPromoPath,
            HasMetaAds = hasMetaAds,
            WebsiteUpdatedRecently = updatedRecently
        };
    }

    private static string NormalizeWebsiteUrl(string websiteUrl)
    {
        var trimmed = websiteUrl.Trim();
        return trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            ? trimmed
            : $"https://{trimmed}";
    }

    [GeneratedRegex(@"fbq\s*\(|facebook\.com/tr|meta\s+pixel", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex MetaPixelRegex();
}
