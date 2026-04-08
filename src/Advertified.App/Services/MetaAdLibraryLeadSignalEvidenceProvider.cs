using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Advertified.App.Configuration;
using Advertified.App.Data.Entities;
using Advertified.App.Services.Abstractions;
using Microsoft.Extensions.Options;

namespace Advertified.App.Services;

public sealed class MetaAdLibraryLeadSignalEvidenceProvider : ILeadSignalEvidenceProvider
{
    private readonly HttpClient _httpClient;
    private readonly AdPlatformOptions _options;
    private readonly ILogger<MetaAdLibraryLeadSignalEvidenceProvider> _logger;

    public MetaAdLibraryLeadSignalEvidenceProvider(
        HttpClient httpClient,
        IOptions<AdPlatformOptions> options,
        ILogger<MetaAdLibraryLeadSignalEvidenceProvider> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<IReadOnlyList<LeadSignalEvidenceInput>> CollectAsync(Lead lead, CancellationToken cancellationToken)
    {
        if (!_options.Meta.Enabled || string.IsNullOrWhiteSpace(_options.Meta.ApiKey))
        {
            return Array.Empty<LeadSignalEvidenceInput>();
        }

        var searchTerm = !string.IsNullOrWhiteSpace(lead.Name) ? lead.Name.Trim() : lead.Website?.Trim();
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return Array.Empty<LeadSignalEvidenceInput>();
        }

        try
        {
            var requestUri =
                $"{_options.Meta.BaseUrl.TrimEnd('/')}/v21.0/ads_archive" +
                $"?search_terms={Uri.EscapeDataString(searchTerm)}" +
                "&ad_reached_countries=%5B%22ZA%22%5D" +
                "&ad_active_status=ACTIVE" +
                "&fields=id,ad_creation_time,page_id,page_name" +
                "&limit=25";

            using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.Meta.ApiKey);
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return Array.Empty<LeadSignalEvidenceInput>();
            }

            var payload = await response.Content.ReadFromJsonAsync<MetaAdsArchiveResponse>(cancellationToken: cancellationToken);
            var adCount = payload?.Data?.Count ?? 0;
            if (adCount <= 0)
            {
                return Array.Empty<LeadSignalEvidenceInput>();
            }

            var observedAt = payload?.Data?
                .Select(item => item.AdCreationTime)
                .Where(value => value.HasValue)
                .Select(value => value!.Value)
                .DefaultIfEmpty(DateTime.UtcNow)
                .Max();

            return new[]
            {
                new LeadSignalEvidenceInput
                {
                    Channel = "social",
                    SignalType = "meta_ad_library_active_ads",
                    Source = "meta_ad_library",
                    Confidence = "detected",
                    Weight = 40,
                    ReliabilityMultiplier = 1.0m,
                    IsPositive = true,
                    ObservedAtUtc = observedAt,
                    EvidenceUrl = "https://www.facebook.com/ads/library/",
                    Value = $"Meta Ad Library returned {adCount} active ad records for the lead."
                }
            };
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or FormatException)
        {
            _logger.LogWarning(ex, "Meta Ad Library evidence collection failed for lead {LeadId}.", lead.Id);
            return Array.Empty<LeadSignalEvidenceInput>();
        }
    }

    private sealed class MetaAdsArchiveResponse
    {
        [JsonPropertyName("data")]
        public List<MetaAdsArchiveItem>? Data { get; init; }
    }

    private sealed class MetaAdsArchiveItem
    {
        [JsonPropertyName("ad_creation_time")]
        public DateTime? AdCreationTime { get; init; }
    }
}
