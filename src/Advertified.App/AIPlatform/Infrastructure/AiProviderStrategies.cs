using System.Text.Json;
using Advertified.App.AIPlatform.Application;
using Advertified.App.AIPlatform.Domain;
using Advertified.App.Configuration;
using Microsoft.Extensions.Options;

namespace Advertified.App.AIPlatform.Infrastructure;

public sealed class OpenAiProviderStrategy : IAiProviderStrategy
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly OpenAIOptions _openAIOptions;
    private readonly ILogger<OpenAiProviderStrategy> _logger;

    public string ProviderName => "OpenAI";

    public OpenAiProviderStrategy(
        IHttpClientFactory httpClientFactory,
        IOptions<OpenAIOptions> openAIOptions,
        ILogger<OpenAiProviderStrategy> logger)
    {
        _httpClientFactory = httpClientFactory;
        _openAIOptions = openAIOptions.Value;
        _logger = logger;
    }

    public bool CanHandle(AdvertisingChannel channel, string operation)
    {
        return operation is "creative-generate" or "creative-qa" or "orchestration";
    }

    public async Task<string> ExecuteAsync(string inputJson, CancellationToken cancellationToken)
    {
        if (!_openAIOptions.Enabled || string.IsNullOrWhiteSpace(_openAIOptions.ApiKey))
        {
            return BuildFallbackResponse(inputJson);
        }

        var requestPayload = JsonSerializer.Deserialize<CreativeProviderInput>(inputJson, SerializerOptions);
        if (requestPayload is null)
        {
            return BuildFallbackResponse(inputJson);
        }

        var baseUrl = string.IsNullOrWhiteSpace(_openAIOptions.BaseUrl)
            ? "https://api.openai.com/v1/"
            : _openAIOptions.BaseUrl.TrimEnd('/') + "/";

        var endpoint = new Uri(new Uri(baseUrl), "chat/completions");
        var requestBody = new
        {
            model = string.IsNullOrWhiteSpace(_openAIOptions.Model) ? "gpt-5-mini" : _openAIOptions.Model,
            temperature = 0.4,
            response_format = new { type = "json_object" },
            messages = new object[]
            {
                new { role = "system", content = requestPayload.SystemPrompt },
                new
                {
                    role = "user",
                    content = $"{requestPayload.UserPrompt}\n\nOutput JSON schema:\n{requestPayload.OutputSchemaJson}\n\nReturn only valid JSON."
                }
            }
        };

        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(JsonSerializer.Serialize(requestBody, SerializerOptions), System.Text.Encoding.UTF8, "application/json")
        };
        requestMessage.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _openAIOptions.ApiKey);

        var client = _httpClientFactory.CreateClient(nameof(OpenAiProviderStrategy));
        if (_openAIOptions.TimeoutSeconds > 0)
        {
            client.Timeout = TimeSpan.FromSeconds(_openAIOptions.TimeoutSeconds);
        }

        try
        {
            using var response = await client.SendAsync(requestMessage, cancellationToken);
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("OpenAI returned non-success status {StatusCode}: {Response}", (int)response.StatusCode, content);
                return BuildFallbackResponse(inputJson);
            }

            using var document = JsonDocument.Parse(content);
            var messageContent = document.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            return EnsureJson(messageContent, inputJson);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OpenAI creative generation failed. Falling back to deterministic payload.");
            return BuildFallbackResponse(inputJson);
        }
    }

    private static string EnsureJson(string? messageContent, string fallbackSource)
    {
        if (!string.IsNullOrWhiteSpace(messageContent))
        {
            try
            {
                JsonDocument.Parse(messageContent);
                return messageContent;
            }
            catch
            {
            }
        }

        return BuildFallbackResponse(fallbackSource);
    }

    private static string BuildFallbackResponse(string inputJson)
    {
        var fallback = new
        {
            headline = "Advertise with confidence",
            body = "AI fallback output generated due to upstream provider unavailability.",
            cta = "Get started today",
            rawInput = inputJson
        };

        return JsonSerializer.Serialize(fallback, SerializerOptions);
    }

    private sealed record CreativeProviderInput(
        string SystemPrompt,
        string UserPrompt,
        string OutputSchemaJson,
        string Channel,
        string Language,
        string TemplateKey);
}

public sealed class ElevenLabsProviderStrategy : IAiProviderStrategy
{
    public string ProviderName => "ElevenLabs";

    public bool CanHandle(AdvertisingChannel channel, string operation)
    {
        return channel == AdvertisingChannel.Radio && operation == "asset-voice";
    }

    public Task<string> ExecuteAsync(string inputJson, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Deserialize<Dictionary<string, object?>>(inputJson) ?? new();
        payload["assetUrl"] = "https://assets.example.com/audio/radio-voice.mp3";
        payload["assetType"] = "voice";
        return Task.FromResult(JsonSerializer.Serialize(payload));
    }
}

public sealed class RunwayProviderStrategy : IAiProviderStrategy
{
    public string ProviderName => "Runway";

    public bool CanHandle(AdvertisingChannel channel, string operation)
    {
        return (channel == AdvertisingChannel.Tv || channel == AdvertisingChannel.Digital) && operation == "asset-video";
    }

    public Task<string> ExecuteAsync(string inputJson, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Deserialize<Dictionary<string, object?>>(inputJson) ?? new();
        payload["assetUrl"] = "https://assets.example.com/video/ad-cut.mp4";
        payload["assetType"] = "video";
        return Task.FromResult(JsonSerializer.Serialize(payload));
    }
}

public sealed class ImageApiProviderStrategy : IAiProviderStrategy
{
    public string ProviderName => "ImageApi";

    public bool CanHandle(AdvertisingChannel channel, string operation)
    {
        return (channel == AdvertisingChannel.Billboard || channel == AdvertisingChannel.Digital) && operation == "asset-image";
    }

    public Task<string> ExecuteAsync(string inputJson, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Deserialize<Dictionary<string, object?>>(inputJson) ?? new();
        payload["assetUrl"] = "https://assets.example.com/image/ad-visual.png";
        payload["assetType"] = "image";
        return Task.FromResult(JsonSerializer.Serialize(payload));
    }
}

public sealed class MultiAiProviderOrchestrator : IMultiAiProviderOrchestrator
{
    private readonly IAiProviderStrategyFactory _factory;

    public MultiAiProviderOrchestrator(IAiProviderStrategyFactory factory)
    {
        _factory = factory;
    }

    public async Task<string> ExecuteAsync(
        AdvertisingChannel channel,
        string operation,
        string inputJson,
        CancellationToken cancellationToken)
    {
        var strategy = _factory.GetRequired(channel, operation);
        return await strategy.ExecuteAsync(inputJson, cancellationToken);
    }
}

public sealed class AiProviderStrategyFactory : IAiProviderStrategyFactory
{
    private readonly IReadOnlyList<IAiProviderStrategy> _strategies;

    public AiProviderStrategyFactory(IEnumerable<IAiProviderStrategy> strategies)
    {
        _strategies = strategies.ToArray();
    }

    public IAiProviderStrategy GetRequired(AdvertisingChannel channel, string operation)
    {
        return _strategies.FirstOrDefault(item => item.CanHandle(channel, operation))
            ?? throw new InvalidOperationException($"No provider strategy is registered for channel '{channel}' and operation '{operation}'.");
    }
}
