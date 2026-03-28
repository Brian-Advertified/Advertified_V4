using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Advertified.App.Configuration;
using Advertified.App.Contracts.Payments;
using Advertified.App.Services.Abstractions;
using Microsoft.Extensions.Options;

namespace Advertified.App.Services;

public sealed class UpstashQStashWebhookQueueService : IWebhookQueueService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly UpstashQStashOptions _options;

    public UpstashQStashWebhookQueueService(HttpClient httpClient, IOptions<UpstashQStashOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task<bool> EnqueueVodaPayWebhookAsync(QueuedVodaPayWebhookJob job, CancellationToken cancellationToken)
    {
        if (!CanUseQueue())
        {
            return false;
        }

        var targetUrl = _options.VodaPayWebhookJobUrl.Trim();
        var publishPath = $"publish/{Uri.EscapeDataString(targetUrl)}";
        var body = JsonSerializer.Serialize(job, SerializerOptions);

        using var request = new HttpRequestMessage(HttpMethod.Post, publishPath)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.Token);
        request.Headers.Add("Upstash-Retries", Math.Max(0, _options.Retries).ToString());

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        return response.IsSuccessStatusCode;
    }

    private bool CanUseQueue()
    {
        return _options.Enabled
            && !string.IsNullOrWhiteSpace(_options.Token)
            && !string.IsNullOrWhiteSpace(_options.VodaPayWebhookJobUrl);
    }
}
