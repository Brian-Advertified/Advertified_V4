using System.Text.RegularExpressions;
using System.Net;
using System.Net.Sockets;
using Advertified.App.Services.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Advertified.App.Services;

public sealed partial class WebsiteSignalProvider : IWebsiteSignalProvider
{
    private static readonly string[] PromoKeywords = new[]
    {
        "sale",
        "promo",
        "discount",
        "clearance",
        "limited time",
        "coupon",
        "deal",
        "save"
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
        "www.facebook.com/tr"
    };
    private static readonly string[] LinkedInAdMarkers = new[]
    {
        "snap.licdn.com",
        "linkedin insight tag",
        "_linkedin_partner_id"
    };
    private static readonly string[] TikTokAdMarkers = new[]
    {
        "analytics.tiktok.com",
        "tiktok pixel",
        "ttq.track"
    };
    private static readonly string[] AudienceTokens = new[]
    {
        "families",
        "professionals",
        "students",
        "parents",
        "small business owners",
        "smes",
        "entrepreneurs",
        "homeowners",
        "commuters",
        "youth"
    };
    private static readonly string[] GenderTokens = new[]
    {
        "women",
        "female",
        "ladies",
        "men",
        "male",
        "gents"
    };

    private readonly HttpClient _httpClient;
    private readonly ILeadMasterDataService _leadMasterDataService;

    [ActivatorUtilitiesConstructor]
    public WebsiteSignalProvider(HttpClient httpClient, ILeadMasterDataService leadMasterDataService)
    {
        _httpClient = httpClient;
        _leadMasterDataService = leadMasterDataService;
    }

    public WebsiteSignalProvider(HttpClient httpClient)
        : this(httpClient, new FallbackLeadMasterDataService())
    {
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

        if (!await IsSafeWebsiteUriAsync(websiteUri, cancellationToken))
        {
            return new WebsiteSignalResult();
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, websiteUri);
            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return BuildResult(string.Empty, response.Headers.Date?.UtcDateTime, null, _leadMasterDataService.GetTokenSet());
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var lastModified = response.Content.Headers.LastModified?.UtcDateTime;

            return BuildResult(content, lastModified, websiteUri, _leadMasterDataService.GetTokenSet());
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

    private static WebsiteSignalResult BuildResult(
        string content,
        DateTime? lastModifiedUtc,
        Uri? websiteUri,
        LeadMasterTokenSet tokenSet)
    {
        var lowered = content.ToLowerInvariant();
        var hasPromoKeyword = PromoKeywords.Any(keyword => ContainsWholeWord(lowered, keyword))
            || DiscountPatternRegex().IsMatch(content);
        var hasPromoPath = PromoPaths.Any(path =>
            lowered.Contains($"href=\"{path}", StringComparison.OrdinalIgnoreCase)
            || lowered.Contains($"href='{path}", StringComparison.OrdinalIgnoreCase)
            || lowered.Contains(path, StringComparison.OrdinalIgnoreCase));
        var hasMetaAds = MetaAdMarkers.Any(marker => lowered.Contains(marker, StringComparison.OrdinalIgnoreCase))
            || MetaPixelRegex().IsMatch(content);
        var hasLinkedInAdsTag = LinkedInAdMarkers.Any(marker => lowered.Contains(marker, StringComparison.OrdinalIgnoreCase));
        var hasTikTokAdsTag = TikTokAdMarkers.Any(marker => lowered.Contains(marker, StringComparison.OrdinalIgnoreCase));
        var title = TryExtractTitle(content);
        var metaSummary = BuildMetaSummary(content);
        var extractionCorpus = $"{title} {metaSummary} {StripHtml(content)}".ToLowerInvariant();
        var locationHints = ExtractHints(extractionCorpus, tokenSet.LocationTokens);
        var industryHints = ExtractHints(extractionCorpus, tokenSet.IndustryTokens);
        var languageHints = ExtractLanguageHints(content, extractionCorpus, tokenSet.LanguageTokens);
        var audienceHints = ExtractHints(extractionCorpus, AudienceTokens);
        var genderHints = ExtractHints(extractionCorpus, GenderTokens);
        // Only trust explicit document freshness metadata. Date headers are often request-time echoes.
        var updatedRecently = lastModifiedUtc.HasValue && lastModifiedUtc.Value >= DateTime.UtcNow.AddDays(-30);

        if (!hasPromoPath && websiteUri is not null)
        {
            hasPromoPath = PromoPaths.Any(path => websiteUri.AbsolutePath.Contains(path, StringComparison.OrdinalIgnoreCase));
        }

        return new WebsiteSignalResult
        {
            HasPromo = hasPromoKeyword || hasPromoPath,
            HasMetaAds = hasMetaAds,
            WebsiteUpdatedRecently = updatedRecently,
            HasLinkedInAdsTag = hasLinkedInAdsTag,
            HasTikTokAdsTag = hasTikTokAdsTag,
            SourceUrl = websiteUri?.ToString(),
            LastObservedAtUtc = DateTime.UtcNow,
            ExtractedTitle = title,
            LocationHints = locationHints,
            IndustryHints = industryHints,
            LanguageHints = languageHints,
            AudienceHints = audienceHints,
            GenderHints = genderHints
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

    private static async Task<bool> IsSafeWebsiteUriAsync(Uri uri, CancellationToken cancellationToken)
    {
        if (!uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
            && !uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (uri.IsLoopback)
        {
            return false;
        }

        var host = uri.Host.Trim();
        if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (IPAddress.TryParse(host, out var ipAddress))
        {
            return !IsPrivateOrReserved(ipAddress);
        }

        try
        {
            var addresses = await Dns.GetHostAddressesAsync(host, cancellationToken);
            if (addresses.Length == 0)
            {
                return false;
            }

            return addresses.All(address => !IsPrivateOrReserved(address));
        }
        catch (SocketException)
        {
            return false;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
    }

    private static bool IsPrivateOrReserved(IPAddress address)
    {
        if (IPAddress.IsLoopback(address))
        {
            return true;
        }

        if (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
        {
            var bytes = address.GetAddressBytes();
            if (bytes.Length != 4)
            {
                return true;
            }

            if (bytes[0] == 10)
            {
                return true;
            }

            if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
            {
                return true;
            }

            if (bytes[0] == 192 && bytes[1] == 168)
            {
                return true;
            }

            if (bytes[0] == 127)
            {
                return true;
            }

            if (bytes[0] == 169 && bytes[1] == 254)
            {
                return true;
            }

            if (bytes[0] == 0)
            {
                return true;
            }

            return false;
        }

        if (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
        {
            if (address.IsIPv6LinkLocal || address.IsIPv6Multicast || address.IsIPv6SiteLocal)
            {
                return true;
            }

            var bytes = address.GetAddressBytes();
            if (bytes.Length > 0 && (bytes[0] & 0xFE) == 0xFC)
            {
                // Unique local address range fc00::/7
                return true;
            }
        }

        return false;
    }

    [GeneratedRegex(@"fbq\s*\(|facebook\.com/tr|meta\s+pixel", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex MetaPixelRegex();

    [GeneratedRegex(@"\b\d{1,3}%\s*(off|discount)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex DiscountPatternRegex();

    private static bool ContainsWholeWord(string source, string token)
    {
        if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        var escaped = Regex.Escape(token.Trim());
        var regex = new Regex($@"\b{escaped}\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        return regex.IsMatch(source);
    }

    private static IReadOnlyList<string> ExtractHints(string source, IReadOnlyList<string> tokens)
    {
        return tokens
            .Where(token => ContainsWholeWord(source, token))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string? TryExtractTitle(string content)
    {
        var match = Regex.Match(content, "<title>(.*?)</title>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (!match.Success)
        {
            return null;
        }

        var value = WebUtility.HtmlDecode(match.Groups[1].Value).Trim();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static string BuildMetaSummary(string content)
    {
        var matches = Regex.Matches(
            content,
            "<meta\\s+[^>]*(name|property)\\s*=\\s*[\"'](?:description|keywords|og:description|og:title|og:locale)[\"'][^>]*content\\s*=\\s*[\"']([^\"']+)[\"'][^>]*>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        if (matches.Count == 0)
        {
            return string.Empty;
        }

        return string.Join(" ", matches
            .Select(match => WebUtility.HtmlDecode(match.Groups[2].Value).Trim())
            .Where(value => !string.IsNullOrWhiteSpace(value)));
    }

    private static string StripHtml(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return string.Empty;
        }

        var withoutScripts = Regex.Replace(
            content,
            "<script\\b[^<]*(?:(?!<\\/script>)<[^<]*)*<\\/script>",
            " ",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);
        var withoutStyles = Regex.Replace(
            withoutScripts,
            "<style\\b[^<]*(?:(?!<\\/style>)<[^<]*)*<\\/style>",
            " ",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);
        var text = Regex.Replace(withoutStyles, "<[^>]+>", " ");
        return WebUtility.HtmlDecode(text);
    }

    private static IReadOnlyList<string> ExtractLanguageHints(
        string content,
        string extractionCorpus,
        IReadOnlyList<string> languageTokens)
    {
        var hints = new HashSet<string>(ExtractHints(extractionCorpus, languageTokens), StringComparer.OrdinalIgnoreCase);

        var htmlLanguageMatch = Regex.Match(content, "<html[^>]*\\slang\\s*=\\s*[\"']([^\"']+)[\"']", RegexOptions.IgnoreCase);
        if (htmlLanguageMatch.Success)
        {
            var languageTag = htmlLanguageMatch.Groups[1].Value.Trim().ToLowerInvariant();
            if (languageTag.StartsWith("en", StringComparison.OrdinalIgnoreCase))
            {
                hints.Add("english");
            }
            else if (languageTag.StartsWith("af", StringComparison.OrdinalIgnoreCase))
            {
                hints.Add("afrikaans");
            }
        }

        return hints.ToArray();
    }

    private sealed class FallbackLeadMasterDataService : ILeadMasterDataService
    {
        private static readonly LeadMasterTokenSet Tokens = new()
        {
            LocationTokens = Array.Empty<string>(),
            IndustryTokens = Array.Empty<string>(),
            LanguageTokens = Array.Empty<string>()
        };

        public LeadMasterTokenSet GetTokenSet() => Tokens;

        public MasterLocationMatch? ResolveLocation(string? value) => null;

        public MasterIndustryMatch? ResolveIndustry(string? value) => null;

        public MasterIndustryMatch? ResolveIndustryFromHints(IReadOnlyList<string> hints) => null;

        public MasterLanguageMatch? ResolveLanguage(string? value) => null;
    }
}
