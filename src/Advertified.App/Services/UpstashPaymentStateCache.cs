using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Advertified.App.Configuration;
using Advertified.App.Contracts.Payments;
using Advertified.App.Services.Abstractions;
using Microsoft.Extensions.Options;

namespace Advertified.App.Services;

public sealed class UpstashPaymentStateCache : IPaymentStateCache
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly UpstashRedisOptions _options;

    public UpstashPaymentStateCache(HttpClient httpClient, IOptions<UpstashRedisOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task SetAsync(string paymentReference, PaymentStateCacheEntry entry, CancellationToken cancellationToken)
    {
        if (!CanUseCache() || string.IsNullOrWhiteSpace(paymentReference))
        {
            return;
        }

        var cacheKey = BuildPaymentKey(paymentReference);
        var payload = JsonSerializer.Serialize(new object[]
        {
            "SETEX",
            cacheKey,
            Math.Max(60, _options.DefaultTtlSeconds),
            JsonSerializer.Serialize(entry, SerializerOptions)
        }, SerializerOptions);

        using var request = BuildRequest(payload);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<PaymentStateCacheEntry?> GetAsync(string paymentReference, CancellationToken cancellationToken)
    {
        if (!CanUseCache() || string.IsNullOrWhiteSpace(paymentReference))
        {
            return null;
        }

        var payload = JsonSerializer.Serialize(new object[]
        {
            "GET",
            BuildPaymentKey(paymentReference)
        }, SerializerOptions);

        using var request = BuildRequest(payload);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        using var json = JsonDocument.Parse(responseBody);
        if (!json.RootElement.TryGetProperty("result", out var resultElement) || resultElement.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        var raw = resultElement.GetString();
        return string.IsNullOrWhiteSpace(raw)
            ? null
            : JsonSerializer.Deserialize<PaymentStateCacheEntry>(raw, SerializerOptions);
    }

    private HttpRequestMessage BuildRequest(string payload)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, string.Empty)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.RestToken);
        return request;
    }

    private bool CanUseCache()
    {
        return _options.Enabled
            && !string.IsNullOrWhiteSpace(_options.RestUrl)
            && !string.IsNullOrWhiteSpace(_options.RestToken);
    }

    private static string BuildPaymentKey(string paymentReference)
    {
        var normalized = paymentReference.Trim();
        return $"payment:{normalized}";
    }
}
