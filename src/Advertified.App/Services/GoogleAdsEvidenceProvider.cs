using Advertified.App.Configuration;
using Advertified.App.Data.Entities;
using Advertified.App.Services.Abstractions;
using Microsoft.Extensions.Options;

namespace Advertified.App.Services;

public sealed class GoogleAdsEvidenceProvider : ILeadSignalEvidenceProvider
{
    private readonly HttpClient _httpClient;
    private readonly AdPlatformOptions _options;
    private readonly ILogger<GoogleAdsEvidenceProvider> _logger;

    public GoogleAdsEvidenceProvider(
        HttpClient httpClient,
        IOptions<AdPlatformOptions> options,
        ILogger<GoogleAdsEvidenceProvider> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<IReadOnlyList<LeadSignalEvidenceInput>> CollectAsync(Lead lead, CancellationToken cancellationToken)
    {
        if (!_options.GoogleAds.Enabled)
        {
            return Array.Empty<LeadSignalEvidenceInput>();
        }

        var searchTerm = !string.IsNullOrWhiteSpace(lead.Website) ? lead.Website!.Trim() : lead.Name.Trim();
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return Array.Empty<LeadSignalEvidenceInput>();
        }

        var transparencyUrl = $"https://adstransparency.google.com/?region=ZA&domain={Uri.EscapeDataString(searchTerm)}";
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, transparencyUrl);
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return Array.Empty<LeadSignalEvidenceInput>();
            }

            var html = await response.Content.ReadAsStringAsync(cancellationToken);
            var lowered = html.ToLowerInvariant();
            var containsAdMarkers =
                lowered.Contains("adstransparency", StringComparison.Ordinal)
                && (lowered.Contains("ad", StringComparison.Ordinal) || lowered.Contains("advertiser", StringComparison.Ordinal));
            if (!containsAdMarkers)
            {
                return Array.Empty<LeadSignalEvidenceInput>();
            }

            return new[]
            {
                new LeadSignalEvidenceInput
                {
                    Channel = "search",
                    SignalType = "google_ads_transparency_signal",
                    Source = "google_ads_transparency",
                    Confidence = "strongly_inferred",
                    Weight = 30,
                    ReliabilityMultiplier = 0.9m,
                    IsPositive = true,
                    ObservedAtUtc = DateTime.UtcNow,
                    EvidenceUrl = transparencyUrl,
                    Value = "Google Ads Transparency endpoint returned advertiser-related ad markers."
                }
            };
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Google Ads evidence collection failed for lead {LeadId}.", lead.Id);
            return Array.Empty<LeadSignalEvidenceInput>();
        }
    }
}
