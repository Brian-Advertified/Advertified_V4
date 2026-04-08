using System.Net.Http.Json;
using Advertified.App.Configuration;
using Advertified.App.Data;
using Advertified.App.Data.Entities;
using Advertified.App.Services.Abstractions;
using Advertified.App.Support;
using Microsoft.Extensions.Options;

namespace Advertified.App.Services;

public sealed class AdPlatformAccessTokenService : IAdPlatformAccessTokenService
{
    private static readonly TimeSpan ExpirySafetyWindow = TimeSpan.FromMinutes(2);
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AdPlatformOptions _options;
    private readonly AppDbContext _db;
    private readonly ILogger<AdPlatformAccessTokenService> _logger;

    public AdPlatformAccessTokenService(
        IHttpClientFactory httpClientFactory,
        IOptions<AdPlatformOptions> options,
        AppDbContext db,
        ILogger<AdPlatformAccessTokenService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _db = db;
        _logger = logger;
    }

    public async Task<string?> ResolveAccessTokenAsync(
        CampaignAdPlatformLink? link,
        string platform,
        CancellationToken cancellationToken)
    {
        var connection = link?.AdPlatformConnection;
        if (connection is null || string.IsNullOrWhiteSpace(connection.AccessToken))
        {
            return null;
        }

        var currentToken = connection.AccessToken.Trim();
        var now = DateTime.UtcNow;
        if (!connection.TokenExpiresAt.HasValue || connection.TokenExpiresAt.Value > now.Add(ExpirySafetyWindow))
        {
            return currentToken;
        }

        if (string.IsNullOrWhiteSpace(connection.RefreshToken))
        {
            _logger.LogWarning(
                "Ad platform token expired for connection {ConnectionId} ({Provider}) but no refresh token is available.",
                connection.Id,
                connection.Provider);
            return currentToken;
        }

        var providerOptions = ResolveProviderOptions(platform);
        if (providerOptions is null || !CanRefresh(providerOptions))
        {
            _logger.LogWarning(
                "Ad platform token refresh is not configured for provider {Provider}; using stored token for connection {ConnectionId}.",
                platform,
                connection.Id);
            return currentToken;
        }

        var refreshed = await RefreshTokenAsync(providerOptions, connection.RefreshToken.Trim(), cancellationToken);
        if (refreshed is null || string.IsNullOrWhiteSpace(refreshed.AccessToken))
        {
            _logger.LogWarning(
                "Ad platform token refresh response did not return an access token for connection {ConnectionId}.",
                connection.Id);
            return currentToken;
        }

        connection.AccessToken = refreshed.AccessToken.Trim();
        if (!string.IsNullOrWhiteSpace(refreshed.RefreshToken))
        {
            connection.RefreshToken = refreshed.RefreshToken.Trim();
        }

        connection.TokenExpiresAt = refreshed.ExpiresAtUtc;
        connection.UpdatedAt = now;
        await _db.SaveChangesAsync(cancellationToken);
        return connection.AccessToken;
    }

    private AdPlatformProviderOptions? ResolveProviderOptions(string platform)
    {
        var normalized = AdPlatformProviderNormalizer.Normalize(platform);
        return normalized switch
        {
            "meta" => _options.Meta,
            "googleads" => _options.GoogleAds,
            _ => null
        };
    }

    private static bool CanRefresh(AdPlatformProviderOptions options)
    {
        return !string.IsNullOrWhiteSpace(options.BaseUrl)
            && !string.IsNullOrWhiteSpace(options.RefreshTokenPath)
            && !string.IsNullOrWhiteSpace(options.ClientId)
            && !string.IsNullOrWhiteSpace(options.ClientSecret);
    }

    private async Task<RefreshTokenResult?> RefreshTokenAsync(
        AdPlatformProviderOptions options,
        string refreshToken,
        CancellationToken cancellationToken)
    {
        var endpoint = options.BaseUrl.TrimEnd('/') + "/" + options.RefreshTokenPath.TrimStart('/');
        var body = new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken,
            ["client_id"] = options.ClientId,
            ["client_secret"] = options.ClientSecret,
        };

        var client = _httpClientFactory.CreateClient(nameof(AdPlatformAccessTokenService));
        client.Timeout = TimeSpan.FromSeconds(Math.Clamp(options.TimeoutSeconds, 5, 120));
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new FormUrlEncodedContent(body),
        };

        var response = await client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var payload = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning(
                "Ad platform token refresh failed with status {Status}. Response: {Response}",
                (int)response.StatusCode,
                payload);
            return null;
        }

        var tokenResponse = await response.Content.ReadFromJsonAsync<TokenRefreshResponse>(cancellationToken: cancellationToken);
        if (tokenResponse is null || string.IsNullOrWhiteSpace(tokenResponse.access_token))
        {
            return null;
        }

        DateTime? expiresAt = null;
        if (tokenResponse.expires_in.HasValue && tokenResponse.expires_in.Value > 0)
        {
            expiresAt = DateTime.UtcNow.AddSeconds(tokenResponse.expires_in.Value);
        }

        return new RefreshTokenResult(
            tokenResponse.access_token,
            tokenResponse.refresh_token,
            expiresAt);
    }

    private sealed record RefreshTokenResult(string AccessToken, string? RefreshToken, DateTime? ExpiresAtUtc);

    private sealed class TokenRefreshResponse
    {
        public string? access_token { get; set; }
        public string? refresh_token { get; set; }
        public int? expires_in { get; set; }
    }
}
